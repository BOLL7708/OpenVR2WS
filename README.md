# OpenVR2WS
WebSocket server that outputs OpenVR/SteamVR data as JSON

This application is a WebSocket server that will provide data from SteamVR to any webpage that connects.
It was made to provide data to a streaming overlay, as a browser source in OBS, but can be used for whatever you find it suitable for.

To open a connection you can use standard JavaScript, there is an ˋexample.htmlˋ file included that will show how it can be done.

The example page is by default using port 7708, as it's the default, but you can set a different port by simply adding ˋ?port=#ˋ where # is your port.

There are a number of commands to send to the application to request data, see the list below.

* (to be filled in asap)

Other things are pushed to the website, like properties changing for connected devices, or changes in input sources, etc.

