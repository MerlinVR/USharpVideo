# USharpVideo
A basic video player made for VRChat using Udon and UdonSharp. Supports normal videos and live streams.

![example](https://i.imgur.com/EZ3imc1.png)

## Features
- Allows master only/everyone lock toggle for video playing
- Video seeking and duration info
- Pause/Play
- Shows master and the last person to play a video
- Default playlist that plays when entering the world
- Stream player
- Support for YouTube timestamped URL's (youtube.com?v=\<video\>&t=\<seconds\>)
- Volume slider

## Installation
1. Install the latest VRCSDK and latest release of [UdonSharp](https://github.com/MerlinVR/UdonSharp/releases/latest)
2. Install the [latest](https://github.com/MerlinVR/USharpVideo/releases/latest) release
2. Drag the USharpVideo prefab into your scene, resize to fit
3. Optionally bake realtime GI for the scene

There is also an example scene with the video player setup with lightmapping and everything in the `USharpVideo/Scenes` directory.
