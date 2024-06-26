# Commonwealth Cartography

The complete mapping tool for Fallout 4.<br/>
A sister of Fallout 76's Mappalachia, Commonwealth Cartography is a Windows application for generating and exporting complex maps of entities within the Fallout 4 game world.

[![Latest release](https://img.shields.io/github/downloads/Mappalachia/Commonwealth_Cartography/total)](https://github.com/Mappalachia/Commonwealth_Cartography/releases/latest)<br>
![](https://img.shields.io/github/last-commit/Mappalachia/Commonwealth_Cartography)<br/>
[![Latest release](https://img.shields.io/github/v/release/Mappalachia/Commonwealth_Cartography)](https://github.com/Mappalachia/Commonwealth_Cartography/releases/latest)<br/>
![](https://img.shields.io/badge/game%20version-1.10.163.0-green)<br/>
[![Discord](https://img.shields.io/discord/1029499482028646400?label=Discord&logo=Discord)](https://discord.gg/Z2GMpm6rad)<br/>
[![License](https://img.shields.io/github/license/Mappalachia/Commonwealth_Cartography)](LICENSE.md)

## Download and Installation

[__Download CommonwealthCartography.zip here__](https://github.com/Mappalachia/Commonwealth_Cartography/releases/latest) to get started generating maps. Simply unzip it to a folder and then launch CommonwealthCartography.exe.<br/>
For help installing please refer to the [installation and first launch guide](User_Guides/Installation_and_first_run.md).<br/>

## Getting started - User Guides

A number of User guides exist for Commonwealth Cartography in document form;<br/>

* [**Installation and First run**](User_Guides/Installation_and_first_run.md) covers initial installation and getting Commonwealth Cartography running.
* [**First map**](User_Guides/First_map.md) explains the basic steps to creating your first Commonwealth Cartography map and other core features.
* [**Customization Options**](User_Guides/Customization.md) covers all the various customization and visual options for your map.
* [**Advanced Searching**](User_Guides/Advanced_searching.md) explains the Scrap and Region search functions, plus misc additional NPC searching, as well as using filters to find exactly what you need.
* [**Advanced Plotting**](User_Guides/Advanced_plotting.md) details the powerful cluster mode, as well as topographical and heatmap plotting, item grouping and volume mapping.
* [**Interiors and other Spaces**](User_Guides/Choosing_spaces.md) explains the mapping of other spaces such as interiors.

## Discord
[Join the Discord for Mappalachia and its sister projects](https://discord.gg/Z2GMpm6rad) for discussion and help.

## Info for Developers

Alongside the source code for the GUI itself, this repository also contains the necessary scripts and code used to export, preprocess and build the Commonwealth Cartography database and supporting image assets.

The required information is compiled in 5 key steps.
1. Extract the raw data in CSV using FO4Edit
2. Refine and preprocess the data
3. Ingest the data into a database
4. (Optional) Image Asset extraction and rendering
5. Image Asset and data validation

If you fancy doing some data mining or development with Commonwealth Cartography then you may be interested in the following documentation;

* [**FO4Edit scripts**](Developer_Guides/EditScripts.md) explains using FO4Edit to run the Commonwealth Cartography edit scripts to export rough, raw game data.
* [**Preprocessor**](Developer_Guides/Preprocessor.md) covers using the CLI tool to process and refine the rough data into suitable CSVs.
* [**Database Ingest**](Developer_Guides/Ingest.md) covers using SQLite to ingest the CSVs into a database which Commonwealth Cartography can read.
* [**Map Icon extraction**](Developer_Guides/IconExtraction.md) explains the process of exporting map marker icons from the game to Commonwealth Cartography.
* [**Background Image Rendering**](Developer_Guides/BackgroundRendering.md) explains using the powerful fo76utils to render top-down views of locations, used for map backgrounds.
* [**Image Asset Validation**](Developer_Guides/ImageAssetValidation.md) walks through how all image assets can be validated ready for a release.
* [**GUI**](Developer_Guides/GUI.md) covers developing the Commonwealth Cartography GUI itself, including how to update Commonwealth Cartography following a new game update.


## Thanks

* Every single person who has so generously donated to say thanks for the Mappalachia project and its forks.
* Contributors to and developers of XEdit and FO76Edit, namely Eckserah.
* Members of the FO76 Datamining Discord, for helping out with FO76Edit and Edit Scripts, and offering valuable knowledge and feedback based on their own experiences datamining and creating Fallout 76 maps.
* [fo76utils](https://github.com/fo76utils) for their excellent and powerful render tool, used to render backgrounds for all cell maps and the Commonwealth satellite map option.
* Gilpo for providing great ideas and feedback for new Mappalachia features.
* Duchess Flame for useful feature feedback, driving community engagement and moral support.
* frame for reporting and helping to test DPI scaling issues.
* Everyone who ever gave feedback to the original Mappalachia. Your feedback, comments, questions, and PMs were essential to defining and guiding the features I have been able to bring to life here.

#### Licensing

This project is licensed under the GNU General Public License 3.0 - see [LICENSE.md](LICENSE.md) for details.<br/>
Commonwealth Cartography uses technologies such as [SQLite](https://www.sqlite.org/index.html) and [SVG.NET](https://github.com/svg-net/SVG) which are each subject to their own licenses.<br/>
Use of other third-party assets are covered below.

#### Legal/Disclaimer

Commonwealth Cartography is provided as a non-commercial, free tool solely for the benefit of players of Fallout 4. Commonwealth Cartography and its creator are neither affiliated with - nor endorsed by - ZeniMax Media or any of its subsidiaries including Bethesda Softworks LLC. Game assets including but not limited to images, characters, names and other game data used for mapping are extracted from a purchased copy of Fallout 4 and are shared here with the game's community in good faith and with an understanding that this lies within fair use.<br/>
Any game data shared here is done so with the explicit purpose of making maps for the benefit of the community, and great care has been taken to minimize such data so that it cannot be reconstructed in any meaningful way.<br/>
If you have any concerns or queries, please direct them to mappalachia.feedback@gmail.com
