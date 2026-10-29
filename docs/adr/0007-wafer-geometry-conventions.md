# Wafer geometry, Die Grid transforms, and Die Index Origin conventions

SiliconDraft maps a configured Die Grid onto a circular wafer and exports die coordinates that YMS analyses can compare. These conventions are recorded because changing them would change every generated Wafer Map and any golden files built on it.

The physical frame has its origin at the wafer center, +X to the right, and +Y up. The Notch angle is counter-clockwise from +X; the default 270° puts the notch at the bottom. Notch orientation is recorded and used by map views; it does not change die validity because the usable region is circular.

Die centers lie on the lattice `gridOffset + (i × pitchWidth, j × pitchHeight)`, with Die Pitch defaulting to die size plus scribe. Mirroring negates X and/or Y about the wafer center, then rotation (0/90/180/270 counter-clockwise) is applied about the wafer center; the die footprints transform with the grid.

A die is valid when its entire footprint lies inside the wafer radius minus Edge Exclusion. A die that is not valid but intersects the physical wafer is a Partial Die. Partial Die are excluded by default; when retained they are exported as untested (`partial`) and never count toward Gross Die. Gross Die counts valid die; Net Die counts CP-passing die. Until the CP step lands, every valid die passes, so Net Die equals Gross Die. The manifest records the Partial Die found (`partialDie`) and the number exported (`partialDieRetained`) separately.

Die coordinates are indexed along the transformed grid's column and row axes, so mirroring and rotation relabel the map even when the physical die set is symmetric. `center` labels the valid die nearest the wafer center (0,0). Corner origins use the valid map's bounding box in the transformed grid frame: `bottomLeft` yields non-negative coordinates and `topRight` non-positive ones, with the other corners mixing signs. Anchors use valid die only, so retaining Partial Die never renumbers the tested map.

Geometry is validated before generation: non-positive sizes, an Edge Exclusion that leaves no usable radius, a pitch below its die dimension, an unsupported rotation or index origin, a grid with no valid die, and a grid exceeding 1,000,000 candidate positions are rejected.

Alternatives: assigning coordinates in the raw wafer X/Y frame (rotating a centered lattice would be unobservable, defeating the YMS-orientation setting); anchoring corner origins on an actual edge die (coordinates depend on the irregular wafer edge); counting retained Partial Die in Gross Die (inflates the yield denominator).
