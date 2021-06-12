
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components;
using VRC.SDKBase;

namespace UdonSharp.Video.Internal
{
    /// <summary>
    /// Allows people to put in links to YouTube videos and other supported video services and have links just work
    /// Hooks into VRC's video player URL resolve callback and uses the VRC installation of YouTubeDL to resolve URLs in the editor.
    /// </summary>
    public static class EditorURLResolverShim
    {
        static string youtubeDLPath = "";
        static HashSet<System.Diagnostics.Process> runningYTDLProcesses = new HashSet<System.Diagnostics.Process>();
        static HashSet<MonoBehaviour> registeredBehaviours = new HashSet<MonoBehaviour>();
        static DateTime lastRequestTime = DateTime.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void SetupURLResolveCallback()
        {
            string[] splitPath = Application.persistentDataPath.Split('/', '\\');
            youtubeDLPath = string.Join("\\", splitPath.Take(splitPath.Length - 2)) + "\\VRChat\\VRChat\\Tools\\youtube-dl.exe";
            //youtubeDLPath = "D:/Merlin/Desktop/youtube-dl.exe";
            
            if (!File.Exists(youtubeDLPath))
            {
                Debug.LogWarning("[USharpVideo YTDL] Unable to find VRC YouTube-dl installation, URLs will not be resolved.");
                return;
            }

            VRCUnityVideoPlayer.StartResolveURLCoroutine = ResolveURLCallback;
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }

        /// <summary>
        /// Cleans up any remaining YTDL processes from this play.
        /// In some cases VRC's YTDL has hung indefinitely eating CPU so this is a precaution against that potentially happening.
        /// </summary>
        /// <param name="change"></param>
        private static void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                foreach (var process in runningYTDLProcesses)
                {
                    if (!process.HasExited)
                    {
                        //Debug.Log("Closing YTDL process");
                        process.Close();
                    }
                }

                runningYTDLProcesses.Clear();

                // Apparently the URLResolveCoroutine will run after this method in some cases magically. So don't because the process will throw an exception.
                foreach (MonoBehaviour behaviour in registeredBehaviours)
                    behaviour.StopAllCoroutines();

                registeredBehaviours.Clear();
            }
        }

        static void ResolveURLCallback(VRCUrl url, int resolution, UnityEngine.Object videoPlayer, Action<string> urlResolvedCallback, Action<VideoError> errorCallback)
        {
            // Broken for some unknown reason, when multiple rate limits fire off, only fires the first callback.
            //if ((System.DateTime.UtcNow - lastRequestTime).TotalSeconds < 5.0)
            //{
            //    Debug.LogWarning("Rate limited " + videoPlayer, videoPlayer);
            //    errorCallback(VideoError.RateLimited);
            //    return;
            //}

            lastRequestTime = System.DateTime.UtcNow;

            System.Diagnostics.Process ytdlProcess = new System.Diagnostics.Process();

            ytdlProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            ytdlProcess.StartInfo.CreateNoWindow = true;
            ytdlProcess.StartInfo.UseShellExecute = false;
            ytdlProcess.StartInfo.RedirectStandardOutput = true;
            ytdlProcess.StartInfo.FileName = youtubeDLPath;
            ytdlProcess.StartInfo.Arguments = $"--no-check-certificate --no-cache-dir --rm-cache-dir -f \"mp4[height<=?{resolution}]/best[height<=?{resolution}]\" --get-url \"{url}\"";

            Debug.Log($"[<color=#9C6994>USharpVideo YTDL</color>] Attempting to resolve URL '{url}'");

            ytdlProcess.Start();
            runningYTDLProcesses.Add(ytdlProcess);

            ((MonoBehaviour)videoPlayer).StartCoroutine(URLResolveCoroutine(url.ToString(), ytdlProcess, videoPlayer, urlResolvedCallback, errorCallback));

            registeredBehaviours.Add((MonoBehaviour)videoPlayer);
        }

        static IEnumerator URLResolveCoroutine(string originalUrl, System.Diagnostics.Process ytdlProcess, UnityEngine.Object videoPlayer, Action<string> urlResolvedCallback, Action<VideoError> errorCallback)
        {
            while (!ytdlProcess.HasExited)
                yield return new WaitForSeconds(0.1f);

            runningYTDLProcesses.Remove(ytdlProcess);

            string resolvedURL = ytdlProcess.StandardOutput.ReadLine();

            // If a URL fails to resolve, YTDL will send error to stderror and nothing will be output to stdout
            if (string.IsNullOrEmpty(resolvedURL))
                errorCallback(VideoError.InvalidURL);
            else
            {
                Debug.Log($"[<color=#9C6994>USharpVideo YTDL</color>] Successfully resolved URL '{originalUrl}' to '{resolvedURL}'");
                urlResolvedCallback(resolvedURL);
            }
        }
    }
}
