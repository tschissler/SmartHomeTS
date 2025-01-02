from pathlib import Path
from typing import Dict, Optional
from bimmer_connected.account import MyBMWAccount
import json

def load_oauth_store_from_file(oauth_store: Path, account: MyBMWAccount) -> Dict:
    """Load the OAuth details from a file if it exists."""
    if not oauth_store.exists():
        print(f"OAuth store file {oauth_store} does not exist")
        return {}
    try:
        oauth_data = json.loads(oauth_store.read_text())
    except json.JSONDecodeError:
        return {}

    session_id_timestamp = oauth_data.pop("session_id_timestamp", None)
    # Pop session_id every 14 days to it gets recreated
    if (time.time() - (session_id_timestamp or 0)) > 14 * 24 * 60 * 60:
        oauth_data.pop("session_id", None)
        session_id_timestamp = None

    account.set_refresh_token(**oauth_data)

    return {**oauth_data, "session_id_timestamp": session_id_timestamp}

def store_oauth_store_to_file(
    oauth_store: Path, account: MyBMWAccount, session_id_timestamp: Optional[float] = None
) -> None:
    """Store the OAuth details to a file."""
    oauth_store.parent.mkdir(parents=True, exist_ok=True)
    oauth_store.write_text(
        json.dumps(
            {
                "refresh_token": account.config.authentication.refresh_token,
                "gcid": account.config.authentication.gcid,
                "access_token": account.config.authentication.access_token,
                "session_id": account.config.authentication.session_id,
                "session_id_timestamp": session_id_timestamp or time.time(),
            }
        ),
    )
