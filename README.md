# OpenVR2WS
WebSocket server that provides SteamVR data as JSON, download the latest release [here](https://github.com/BOLL7708/OpenVR2WS/releases).

This application is a WebSocket server that will provide data from SteamVR to any webpage that connects.
It was made to provide data to a streaming overlay, as a browser source in OBS, but can be used for whatever you find it suitable for.
To load data you can use standard JavaScript in a modern browser.

## Usage
For a working client implementation see `example.html` included in the release, there is a `Example`-button to open it in the application as well. It is using vanilla JavaScript to open a websocket connection to this application. 

The page is by default using port `7708` as it's the default for the server, but if you have changed the port in the application you can use that with the example page by adding ˋ?port=#ˋ to the URL where # is your port.

## Commands
There are a number of commands to send to the server to request data, see below.

### Format
To the server, send a JSON package with this format 

    {
      "key":"the_command",
      "device":"device_index",
      "value":"a_value",
      "value2":"another_value"
    }

### Available Commands
These are the keys to be used.
* CumulativeStats : Loads current cumulative stats, basically frame data.
* PlayArea : Loads current play area data.
* ApplicationInfo : Information regarding the currently running application.
* DeviceIds : Information regarding currently connected devices, where you get the device indices.
* DeviceProperty : Specific device properties, supply the device index, and the key for the property as value.
* InputAnalog : Float values for bound analog inputs.
* InputPose : Pose data for connected devices.
* Setting : Load SteamVR setting, section as value, setting as value2.

## Events
Certain events from SteamVR gets pushed over the connection, things like changing device properties or changes in input sources, these will arrive without you having to request them.
