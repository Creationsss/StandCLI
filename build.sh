#!/bin/sh

#./build.sg -w runs wine after the build is complete
#./build.sg -c copies the executable file next to the script

current_folder="$(pwd)"
script_name="$0" # Get the name of the running script
dotnet_publish_command="dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true"
dotnet_publish_output=$($dotnet_publish_command)

if [ $? -eq 0 ]; then
  echo "dotnet publish completed successfully."

  output_location=$(echo "$dotnet_publish_output" | grep -o "$current_folder/bin/Release/net6.0/win-x64/publish/")
  executable_file=$(find "$output_location" -name "*.exe")

  if [ -n "$output_location" ]; then
    echo "Output location: $output_location"
  else
    echo "Output location not found in the dotnet publish output."
  fi

  if [ -n "$executable_file" ]; then
    echo "Executable file: $executable_file"
    while getopts ":wc" opt; do
      case $opt in
        c)
        echo "Copying the executable file next to the script..."
        cp "$executable_file" "$current_folder/"
        echo "Executable file copied successfully."
          ;;
        w)
        export WINEDEBUG=-all
        wine "$executable_file"
          ;;
        \?)
          echo "Invalid option: -$OPTARG" >&2
          exit 1
          ;;
      esac
    done
  else
    echo "Executable file not found in the publish directory."
  fi
else
  echo "dotnet publish failed."
fi
