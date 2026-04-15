#!/usr/bin/env fish
# Loads variables from .env and sets them in the systemd user environment.
# Run once after updating .env: source set_env.fish

set script_dir (dirname (status --current-filename))
set env_file "$script_dir/.env"

if not test -f $env_file
    echo "No .env file found at $env_file"
    exit 1
end

for line in (cat $env_file)
    # Skip empty lines and comments
    if string match -qr '^\s*$|^\s*#' -- $line
        continue
    end
    set parts (string split -m 1 "=" -- $line)
    if test (count $parts) -eq 2
        set key (string trim $parts[1])
        set value (string trim $parts[2])
        systemctl --user set-environment "$key=$value"
        echo "Set $key"
    end
end

echo "Done. Restart VSCodium if it was already running."
