# Setup Guide - InfluxDB3 Migration Tool

Quick start guide for setting up the migration tool.

## Step 1: Install Dependencies

```bash
# Create virtual environment
python -m venv venv

# Activate virtual environment
# Windows:
venv\Scripts\activate
# Linux/Mac:
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt
```

## Step 2: Configure Environment Variables

### Option A: Using .env File (Recommended)

The tool automatically loads environment variables from a `.env` file.

1. **Copy the example file:**
   ```bash
   copy .env.example .env
   ```

2. **Edit `.env` and fill in your credentials:**
   ```bash
   # Open in your editor
   notepad .env
   ```

3. **Add your tokens:**
   ```env
   INFLUX_SOURCE_TOKEN=your_actual_source_token_here
   INFLUX_TARGET_TOKEN=your_actual_target_token_here
   ```

That's it! The migration tool will automatically load these when you run it.

### Option B: Using System Environment Variables

Alternatively, you can set environment variables in your system:

**Windows:**
```cmd
set INFLUX_SOURCE_TOKEN=your_source_token
set INFLUX_TARGET_TOKEN=your_target_token
```

**Linux/Mac:**
```bash
export INFLUX_SOURCE_TOKEN="your_source_token"
export INFLUX_TARGET_TOKEN="your_target_token"
```

**Or use the helper scripts:**

Windows:
```cmd
load_env.bat
```

Linux/Mac:
```bash
source load_env.sh
```

## Step 3: Configure Migration

1. **Copy the example configuration:**
   ```bash
   copy config.yaml.example config.yaml
   ```

2. **Edit `config.yaml`:**
   ```yaml
   migration:
     name: "My_Migration"
     mode: "incremental"
     dry_run: false

   source:
     host: "https://your-source-host.com"
     database: "your_source_database"
     token: "${INFLUX_SOURCE_TOKEN}"  # Uses env var from .env

   target:
     host: "https://your-target-host.com"
     database: "your_target_database"
     token: "${INFLUX_TARGET_TOKEN}"  # Uses env var from .env

   tables:
     include:
       - "temperature_values"
       - "power_values"
       # Add more tables as needed
   ```

## Step 4: Test Configuration

Run a dry-run to validate everything is working:

```bash
python migrate.py --config config.yaml --dry-run
```

This will:
- ✓ Load environment variables from `.env`
- ✓ Test connections to source and target
- ✓ Count rows to be migrated
- ✓ Display what would be migrated
- ✗ **NOT** write any data

## Step 5: Run Migration

If the dry-run looks good, run the actual migration:

```bash
python migrate.py --config config.yaml
```

## Troubleshooting

### "Environment variable not set" error

**Problem:** You see an error like `Environment variable not set: INFLUX_SOURCE_TOKEN`

**Solutions:**
1. Make sure `.env` file exists (copy from `.env.example`)
2. Check that you filled in the tokens in `.env`
3. Verify no extra spaces around the `=` sign in `.env`
4. Make sure `python-dotenv` is installed: `pip install python-dotenv`

### .env file not loading

**Problem:** Environment variables from `.env` are not being loaded

**Solutions:**
1. Check that `.env` is in the same directory as `migrate.py`
2. Reinstall dependencies: `pip install -r requirements.txt`
3. Manually load using helper scripts:
   - Windows: `load_env.bat`
   - Linux/Mac: `source load_env.sh`

### How to check if environment variables are loaded

**Windows:**
```cmd
echo %INFLUX_SOURCE_TOKEN%
```

**Linux/Mac:**
```bash
echo $INFLUX_SOURCE_TOKEN
```

If it shows your token, it's loaded correctly.

## Security Notes

- ✓ `.env` is in `.gitignore` - won't be committed to git
- ✓ `config.yaml` is in `.gitignore` - won't be committed to git
- ✓ `.env.example` has placeholders only - safe to commit
- ✓ Tokens are loaded at runtime, not stored in code

## Quick Reference

| File | Purpose | Commit to Git? |
|------|---------|----------------|
| `.env.example` | Template for environment variables | ✓ Yes |
| `.env` | Your actual credentials | ✗ No - ignored |
| `config.yaml.example` | Template for configuration | ✓ Yes |
| `config.yaml` | Your actual configuration | ✗ No - ignored |
| `load_env.bat` | Helper script (Windows) | ✓ Yes |
| `load_env.sh` | Helper script (Linux/Mac) | ✓ Yes |

## Next Steps

Once setup is complete, see [README.md](README.md) for:
- Detailed usage examples
- Performance tuning
- Troubleshooting
- Best practices
