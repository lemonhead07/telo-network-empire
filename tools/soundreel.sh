#!/usr/bin/env bash
# Render the game's synthesised sounds to .wav and draw a spectrogram of each (EA7).
#   tools/soundreel.sh <outdir> [id-prefix|family ...]
# Then listen to <outdir>/reel.wav, or look at <outdir>/<id>.png (time across, 0-11 kHz up, loudness as brightness).
set -euo pipefail
cd "$(git rev-parse --show-toplevel)"
out=$(realpath -m "${1:?usage: tools/soundreel.sh <outdir> [filter ...]}"); shift
dotnet run --project tools/soundreel -c Release -- "$out" "$@"
if command -v ffmpeg >/dev/null; then
  for w in "$out"/*.wav; do
    [ "$(basename "$w")" = reel.wav ] && continue
    ffmpeg -v error -y -i "$w" -lavfi "showspectrumpic=s=640x240:legend=1:stop=11000:scale=log:color=intensity" "${w%.wav}.png" || true
  done
  echo "spectrograms: $out/*.png"
fi
