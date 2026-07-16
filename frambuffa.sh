#!/bin/sh
printf '\033c\033]0;%s\a' frambuffa
base_path="$(dirname "$(realpath "$0")")"
"$base_path/frambuffa.x86_64" "$@"
