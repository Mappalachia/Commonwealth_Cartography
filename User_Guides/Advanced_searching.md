# Advanced Commonwealth Cartography Search functions

## Search filters
Under the 'Standard Search' tab, you will have noticed two filters - 'Category' and 'Lock Level'.<br/>
By removing check marks from the boxes here, you can exclude certain types of items from your search. For example, searching "cat" with no filters can yield many different results, but if you press 'Deselect all' from the 'Category' group, and then re-check 'NPC' you will find a much narrower results set.<br/>
You can also, for example only check 'Level 3' under 'Lock Level' and search 'Safe' to find just safes which have a level 3 lock.<br/>
Combining these different filters can be very powerful.<br/>

Note: Due to oddities in how Fallout 4 is built, a great number of items are categorized in-game as 'Loot' (notably, most natural resources). Due to this, it is generally recommended to leave the 'Loot' filter checked if you're uncertain.

## Search field
The search field operates as a case-insensitive 'contains' search - meaning anything with your search term anywhere within it will be returned, for example "cap" will return results for "FloraFirecap".<br/>
This searches both the in-game displayed name (where applicable), Bethesda's internal name for the object (AKA EditorID), and the "label" of the instance (AKA Reference EditorID).<br/>
Additionally the space character is treated as a wildcard for any number of characters, meaning "Grafton Monster" will be able to return matches for "GraftonMonster".<br/>
Leaving the search field blank will return everything (while still respecting selected filters).<br/>
Finally, data miners and modders will find that you are able to search for items via their FormID too. You can view the FormID for returned items by selecting Search Settings > Show FormID.<br/>

## Searching in all Spaces
By navigating to Search Settings and toggling on 'Search in all Spaces' you can see search results from every accessible location in-game, both surface world and indoors.<br/>
You cannot however plot items which belong to a different space than the one currently selected. To learn more about making maps for other spaces, please see [Interiors and other Spaces](Choosing_spaces.md).

## Region Search
The final item on the Region/Scrap/NPC Search tab is Region search. This niche feature allows you to search for and plot defined world regions.<br/>
These regions generally represent biome boundaries, workshop borders, the playable zone, and the non-nuke zone in the forest.<br/>
The search field here behaves the same as the standard search field - in that it will return anything containing your term, and you can leave it empty to return all results. It can also search for the FormID of the region.</br>
Region search is distinct from volume plotting, as regions are complex polygons (sometimes multiple) - they also have infinite height. Due to this, they ignore the current plotting mode as they cannot be represented by a single position. However, their color can still be customized by changing the legend group.<br/>

## Scrap Search
Again, by selecting the 'Region/Scrap/NPC Search' tab you can also search for junk items which contain a given scrap material.<br/>
This feature does not search directly for *junk* but specifically the *scrap* contents of the junk, once they're broken down. The search accounts for both the contents of the junk, and crucially the *amount* of scrap that can be obtained.<br/>
The scrap amount is best visualized in Heatmap mode. See [Advanced Plotting](Advanced_plotting.md) for info on Heatmap mode.<br/>
This function is very simple - simply select your desired scrap and hit search.<br/>
Much like NPC Spawns, the list of available junk is generated dynamically from the in-game data, and any scrap not on the list would indicate that it does not exist naturally in the game world.


## NPC Search
By changing the tab at the top to 'Region/Scrap/NPC Search' you may use a separate search which can find some additional spawns not under the 'standard search'.<br/>
In Fallout 4, the majority of NPCs however should be found under the standard search.<br/>
You will notice that there is no search field here but instead you must select the NPC by name. This list is generated dynamically from the data exported from the game.<br/>

This intelligent NPC Search will also aggregate the search results with a 'Standard Search' for the same name of the NPC category.<br/>
For example, by selecting 'Mirelurk' and searching, you should see the top results on the results list will be the variable spawns where Snallygasters *may* spawn (alongside an indicated spawn chance). Then the following the results will be other matches for Snallygaster - these being non-variable, guaranteed spawns.<br/>
