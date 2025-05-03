### Overview
Space Engineers mod to add commands for showing GPS markers as large 3D spheres in the world. Client side only and originally designed for the Sigma Draconis Expanse server to aid in navigating through space and avoiding the 'slow zones'. Useful for marking gravity wells, danger exclusion zones or any other point in space with a radius.

To use this open up a cockpit in the k-menu and look for the new controls. There are two lists, one for available GPS points and one for active markers. You can create a marker from a properly formatted GPS point using the 'Create from GPS' button. In order for a GPS to show in the available list it's name needs to follow some rules:
- GPS name must contain the range in Kilometers in one of the following formats:
  - `(R-<range>)`
  - `(R:<range>)`
  - `(R.<range>)`
  - `[R-<range>]`
  - `[R:<range>]`
  - `[R.<range>]`
- For example `Danger! (R-10)` as a GPS name would allow it to be selected in the Cockpit/Control seat terminal controls.
- Marker color is take from the GPS color at the time of creation.

### Settings
The menu to adjust some render parameters can be found using HupAPI. Access it by opening chat and then pressing F2. This will put the text 'Mod Settings' in white on the left side of the screen. Click that to open all mod settings and choose 'Nav Markers' from the list.

### Chat Commands
Chat commands can be found by using either '/nav' or '/nm' in the in-game chat box. Example: '/nm help'

Current commands: (More to come depending on feedback)
- add `<range> <name>`: Adds a marker with radius `<range>` centered on the GPS point with `<name>`
  - `<name>` can have spaces but must be surounded in quotes
  - Example: `/nav add 100 "Saturn Rings"`
    - Adds a nav marker with a radius of 100km centered around the GPS point named "Saturn Rings"
- remove `<name>`: Removes a marker with the matching `<name>`
- set `<range> <name>`: Updates an existing nav marker's radius

- list: Lists all active markers in chat
- toggle: Toggle visibility of all markers
- close: Toggle showing only close / nearby markers. Change settings in the F2 Menu
- intersect: Create a new GPS marker at the intersection of the camera ray and active nav markers
- help: Shows a help message

### Source
Mod hosted on steam: https://steamcommunity.com/sharedfiles/filedetails/?id=3363175955

This is actively under development currently and comments are welcome.

**Note: Now available in Plugin Loader
