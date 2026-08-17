#!/bin/bash
set -e
URL="https://builds.dotnet.microsoft.com/dotnet/Sdk/8.0.408/dotnet-sdk-8.0.408-win-x64.zip"
D="/c/Users/Administrator/WorkBuddy/AI/ClasslandCE/.sdkparts"
mkdir -p "$D"
cd "$D"
SIZE=16777216
TOTAL=281538746
i=0
while [ $((i*SIZE)) -lt $TOTAL ]; do
  start=$((i*SIZE))
  end=$((start+SIZE-1))
  if [ $end -ge $TOTAL ]; then end=$((TOTAL-1)); fi
  curl -s --max-time 120 -r ${start}-${end} -o $(printf "part_%03d" $i) "$URL"
  i=$((i+1))
done
cat part_* > /c/Users/Administrator/WorkBuddy/AI/ClasslandCE/dotnet8-sdk.zip
sz=$(wc -c < /c/Users/Administrator/WorkBuddy/AI/ClasslandCE/dotnet8-sdk.zip)
echo "MERGED size=$sz expected=$TOTAL"
if [ "$sz" != "$TOTAL" ]; then echo "SIZE MISMATCH"; exit 1; fi
powershell -NoProfile -Command "Expand-Archive -Path 'C:/Users/Administrator/WorkBuddy/AI/ClasslandCE/dotnet8-sdk.zip' -DestinationPath 'C:/Users/Administrator/dotnet8' -Force"
echo "EXTRACTED"
/c/Users/Administrator/dotnet8/dotnet --list-sdks
