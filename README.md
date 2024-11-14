### Overview
Space Engineers mod to add commands for showing GPS markers as large 3D spheres in the world. Client side only and originally designed for the Sigma Draconis Expanse server to aid in navigating through space and avoiding the 'slow zones'. Useful for marking gravity wells, danger exclusion zones or any other point in space with a radius.

To use this open up a cockpit in the k-menu and look for the new controls. There are two lists, one for available GPS points and one for active markers. You can create a marker from a properly formatted GPS point using the 'Create from GPS' button. In order for a GPS to show in the available list it's name needs to follow some rules:
- GPS name must contain the range in Kilometers in one of the following formats:
  - `(R-<range>)`
  - `(R:<range>)`
  - `[R-<range>]`
  - `[R:<range>]`
- For example `Danger! (R-10)` as a GPS name would allow it to be selected.
- Marker color is take from the GPS color at the time of creation.

### Settings
The menu to adjust some render parameters can be found using HupAPI. Access it by opening chat and then pressing F2. This will put the text 'Mod Settings' in white on the left side of the screen. Click that to open all mod settings and choose 'Nav Markers' from the list.

### Chat Commands

Current commands: (More to come depending on feedback)
- add <range> <name>: Adds a marker at the GPS point with <name> and radius <range>
- remove <name>: Removes a marker with the matching <name>
- list: Lists all active markers in chat
- help: Shows a help message (placeholder for now)

### Source
Mod hosted on steam: https://steamcommunity.com/sharedfiles/filedetails/?id=3363175955

This is actively under development currently and comments are welcome.

**Note: Now available in Plugin Loader
