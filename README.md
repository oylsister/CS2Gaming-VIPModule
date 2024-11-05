# CS2Gaming-VIPModule
 VIP Module for [CS2GamingAPI](https://github.com/oylsister/CS2GamingAPI/), After select specific item from VIP Menu will trigger API Request from CS2GamingAPICore and set cooldown for that feature.

https://www.youtube.com/watch?v=bJz9z3PU_Os

## Requirement
- [cs2-VIPCore](https://github.com/partiusfabaa/cs2-VIPCore)

## Installation
- Simply drag all content in zip file into ``addons/counterstrikesharp/plugins/

 On plugin load to the server, this plugin will start generate config file at ``addons/counterstrikesharp/configs/plugins/
```jsonc
{
  "RestrictList": ["Respawn", "etc..."] // List of the Feature to be call API after select.
}
```
