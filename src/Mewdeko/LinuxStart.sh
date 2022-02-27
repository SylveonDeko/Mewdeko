#!/bin/sh
# shellcheck disable=SC2113
function fail {
    printf '%s\n' "$1" >&2 ## Send message to stderr.
    exit "${2-1}" ## Return a code specified by $2, or 1 by default.
}
echo "Starting Mewdeko Build..."
sleep 5s
dotnet build -c Release
cd bin/Release/net6.0/ || fail "Looks like the build failed. Please check you have dotnet installed."
FILE=data/Mewdeko.db
if test -f "$FILE" 
then
  echo "Starting Mewdeko..."
  cd ../../..
  dotnet run -c Release
else
  echo "Running first start script..."
  cd data
  wget https://cdn.discordapp.com/attachments/915770282579484693/946967102680621087/Mewdeko.db
  cd ../../../..
  dotnet run -c Release
fi
