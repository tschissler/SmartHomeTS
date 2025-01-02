from kubernetes import client, config
import base64
import time
from typing import Dict, Optional
from bimmer_connected.account import MyBMWAccount 

def load_k8s_config():
    """Load Kubernetes configuration based on the environment."""
    try:
        config.load_incluster_config()
    except config.ConfigException:
        config.load_kube_config()

def load_oauth_store_from_k8s_secret(secret_name: str, namespace: str, account: MyBMWAccount) -> Dict:
    """Read the OAuth details from a Kubernetes secret."""
    print(f"Loading OAuth data from Kubernetes secret {secret_name} in namespace {namespace}")
    load_k8s_config()
    v1 = client.CoreV1Api()

    try:
        secret = v1.read_namespaced_secret(secret_name, namespace)
        secret_data = secret.data

        oauth_data = {
            "refresh_token": base64.b64decode(secret_data["refresh_token"]).decode('utf-8'),
            "gcid": base64.b64decode(secret_data["gcid"]).decode('utf-8'),
            "access_token": base64.b64decode(secret_data["access_token"]).decode('utf-8'),
            "session_id": base64.b64decode(secret_data["session_id"]).decode('utf-8'),
            "session_id_timestamp": float(base64.b64decode(secret_data["session_id_timestamp"]).decode('utf-8')),
        }

        session_id_timestamp = oauth_data.pop("session_id_timestamp", None)
        if (time.time() - (session_id_timestamp or 0)) > 14 * 24 * 60 * 60:
            oauth_data.pop("session_id", None)
            session_id_timestamp = None

        account.set_refresh_token(**oauth_data)
        
        return {**oauth_data, "session_id_timestamp": session_id_timestamp}

    except client.exceptions.ApiException as e:
        if e.status == 404:
            raise ValueError(f"Secret {secret_name} not found in namespace {namespace}")
        else:
            raise e

def store_oauth_store_to_k8s_secret(
    secret_name: str, namespace: str, account: MyBMWAccount, session_id_timestamp: Optional[float] = None
) -> None:
    """Store the OAuth details in a Kubernetes secret."""
    load_k8s_config()
    v1 = client.CoreV1Api()

    secret_data = {
        "refresh_token": base64.b64encode(account.config.authentication.refresh_token.encode()).decode('utf-8'),
        "gcid": base64.b64encode(account.config.authentication.gcid.encode()).decode('utf-8'),
        "access_token": base64.b64encode(account.config.authentication.access_token.encode()).decode('utf-8'),
        "session_id": base64.b64encode(account.config.authentication.session_id.encode()).decode('utf-8'),
        "session_id_timestamp": base64.b64encode(str(session_id_timestamp or time.time()).encode()).decode('utf-8'),
    }

    secret = client.V1Secret(
        metadata=client.V1ObjectMeta(name=secret_name),
        data=secret_data,
    )

    try:
        v1.read_namespaced_secret(secret_name, namespace)
        v1.replace_namespaced_secret(secret_name, namespace, secret)
    except client.exceptions.ApiException as e:
        if e.status == 404:
            v1.create_namespaced_secret(namespace, secret)
        else:
            raise e