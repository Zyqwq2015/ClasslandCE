#!/bin/bash
set -e
URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/9.0.317/dotnet-sdk-9.0.317-win-x64.zip"
D="/c/Users/Administrator/WorkBuddy/AI/ClasslandCE/.sdk9parts"
mkdir -p "$D"
cd "$D"
TOTAL=$(curl -sI --max-time 30 "$URL" | grep -i content-length | tr -dc '0-9')
echo "TOTAL=$TOTAL"
SIZE=16777216
i=0
while [ $((i*SIZE)) -lt "$TOTAL" ]; do
  start=$((i*SIZE)); end=$((start+SIZE-1))
  if [ $end -ge "$TOTAL" ]; then end=$((TOTAL-1)); fi
  curl -s --max-time 120 -r ${start}-${end} -o $(printf "part_%03d" $i) "$URL"
  i=$((i+1))
done
cat part_* > /c/Users/Administrator/dotnet9-sdk.zip
sz=$(wc -c < /c/Users/Administrator/dotnet9-sdk.zip)
echo "MERGED=$sz EXPECT=$TOTAL"
if [ "$sz" != "$TOTAL" ]; then echo "MISMATCH"; exit 1; fi
powershell -NoProfile -Command "Expand-Archive -Path 'C:/Users/Administrator/dotnet9-sdk.zip' -DestinationPath 'C:/Users/Administrator/dotnet9' -Force"
echo "EXTRACTED"
/c/Users/Administrator/dotnet9/dotnet --list-sdks
