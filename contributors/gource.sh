#!/usr/bin/env bash

# This script generates a video of the contributors to the project using gource.

# current shell script directory
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# generate the activity log
gource --output-custom-log "${DIR}"/gource.txt

# replace the names in the activity log
# bug. it is not replacing this case: "Nominal9977|Justin Ward"
(IFS="|"
while read wrong_name name; do
  sed -i "s/|$wrong_name|/|$name|/g" "${DIR}"/gource.txt
done < "${DIR}"/name_replacements.txt)

# generate the video
gource -1920x1080 --caption-file "${DIR}"/captions.txt --title GameGuild --user-image-dir "${DIR}" --auto-skip-seconds 0.1 --multi-sampling --stop-at-end --key --highlight-users --hide mouse,filenames --file-idle-time 0 --max-files 0 --seconds-per-day 0.1 --user-scale 2.0 --bloom-multiplier 0.5 --output-ppm-stream - "${DIR}"/gource.txt | ffmpeg -y -r 60 -f image2pipe -vcodec ppm -i - -vcodec libx264 -crf 28 -pix_fmt yuv420p -threads 0 -bf 0 "${DIR}"/gource.mp4

ffmpeg -i "${DIR}"/gource.mp4 -ss 5 -loop 0 -filter_complex "fps=10, scale=-1:360[s]; [s]split[a][b]; [a]palettegen[palette]; [b][palette]paletteuse" "${DIR}"/gource.gif
