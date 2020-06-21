# OpenVR2WS
WebSocket server that outputs OpenVR/SteamVR data as JSON

This application is a WebSocket server that will provide data from SteamVR to any webpage that connects.
It was made to provide data to a streaming overlay, as a browser source in OBS, but can be used for whatever you find it suitable for.

To open a connection you can use standard JavaScript, there is an ˋexample.htmlˋ file included that will show how it can be done.

The example page is by default using port `7708`, as it's the default, but you can set a different port by simply adding ˋ?port=#ˋ where # is your port.

## Commands
There are a number of commands to send to the application to request data, see below.

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

Other things that arrive through events are pushed to connected webpages, things like changing device properties or changes in input sources, etc.

