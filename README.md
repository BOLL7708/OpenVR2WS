# OpenVR2WS
This is a WebSocket server that provides SteamVR I/O as JSON; allows for fetching of various kinds of data, as well as providing changes to SteamVR settings when sending data upstream. Download the latest release [here](https://github.com/BOLL7708/OpenVR2WS/releases).

If you want to chat about this application, or discuss solutions involving it, feel free to join the [Discord](https://discord.gg/Cdt4xjqV35) server for my projects.

## Usage
* [OpenVR2WS-Types](https://www.npmjs.com/package/openvr2ws-types) is a package on NPM with all the requests and responses provided as TypeScript types.
* [OpenVR2WS-Example](https://github.com/BOLL7708/openvr2ws-example) is an example client implementation that communicates with this application.

Outside of references and examples, the server is connected to on the IP or name of the host, using the supplied port where `7708` is the default. A typical connection URI would be: `ws://localhost:7708`

Then you send and receive JSON messages to it! It's fairly straight forward, see message examples below. 

## Messages
In the `types` package above you can find all available payloads and messages. 
### Requests
Input messages are requests sent to the server to retrieve on-demand data or to remotely update SteamVR values.  
Example:
```json
{
  "Key": "RemoteSetting",
  "Password": "your_encoded_password_hash",
  "Data": {
    "Section": "steam.app.2494440",
    "Setting": "worldScale",
    "Value": "2"
  },
  "Nonce": "YourOneTimeReference"
}
```
Here the `Data` value is flexible, and can be any of the `Data*` classes found in the project, or in the types.
### Responses
Output messages are the responses sent to the client from the server, either automatically due to SteamVR events, or as a result after a data request.  
Example:
```json
{
  "Type": "Message",
  "Key": "RemoteSetting",
  "Message": "Succeeded setting steam.app.2494440/worldScale to 1.",
  "Data": null,
  "Nonce": "YourOneTimeReference"
}
```
Just like above, the `Data` field in this response object is flexible, and can be any of the `Json*` classes found in the project, or in the types. In this example it's empty, and as such set to `null`.

## Projects that use OpenVR2WS
Here is a non-exhaustive list of projects that rely on this application:
* [Desbot](https://github.com/BOLL7708/desbot)

If you have a project you want to see on this list, open an issue and I'll check it out 🙂