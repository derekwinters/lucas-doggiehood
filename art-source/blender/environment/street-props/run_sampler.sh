#!/bin/bash
# render the four sampler panels (2 at a time) and compose the sheet into
# out/review/ (git-ignored); copy sampler-street-props.png to review/ to update it
mkdir -p "$(dirname "$0")/out/review"
cd "$(dirname "$0")/out/review"
R=../../render_props_sampler.py
for p in lights signs; do timeout 900 python3 $R panel $p p_$p.png > p_$p.log 2>&1 & done; wait
for p in furniture decals; do timeout 900 python3 $R panel $p p_$p.png > p_$p.log 2>&1 & done; wait
python3 $R compose sampler-street-props.png p_lights.png p_signs.png p_furniture.png p_decals.png
echo SAMPLER DONE
