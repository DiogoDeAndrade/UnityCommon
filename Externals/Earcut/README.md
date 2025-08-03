This was taken from [EarCut](https://github.com/MadWorldNL/EarCut) and adapted for C# 9 by me.

This is used by the Polyline class to generate a triangulation from a boundary (the polyline) and some additional holes. 
The code in EarCut supports Steiner points, but I don't expose them on the Polyline class.
