# CS2Gaming-VIPModule
 VIP Item Module for [CS2GamingAPI](https://github.com/oylsister/CS2GamingAPI/), After select CSGamingItem from VIP Menu will trigger API Request from CS2GamingAPICore and set cooldown for that feature.

[![Video](https://img.youtube.com/vi/bJz9z3PU_Os/maxresdefault.jpg)](https://www.youtube.com/watch?v=bJz9z3PU_Os)

## Requirement
- [cs2-VIPCore](https://github.com/partiusfabaa/cs2-VIPCore)
- [CS2GamingAPI](https://github.com/oylsister/CS2GamingAPI/)

## Installation
- Simply drag all content in zip file into ``addons/counterstrikesharp/plugins/``

 Editing VIP config file at ``addons/counterstrikesharp/configs/plugins/VIPCore/vips.json``
```jsonc
{
  "Delay": 2,
  "Groups": {
    "VIP": { // VIP group name
      "Values": {
        "CS2GamingItem": true // set to true for this group to receive item.
      }
    }
  }
}
```
