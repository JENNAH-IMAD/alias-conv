"""Encrypted PostgreSQL + uploads transfer. Run while the application is stopped."""
import argparse
import getpass
import io
import os
from pathlib import Path
import re
import shutil
import subprocess
import tempfile
import zipfile

from cryptography.hazmat.primitives.ciphers.aead import AESGCM
from cryptography.hazmat.primitives.kdf.scrypt import Scrypt

ROOT = Path(__file__).resolve().parents[1]
MAGIC = b"RFLOW01"


def key(password, salt):
    return Scrypt(salt=salt, length=32, n=2**17, r=8, p=1).derive(password.encode())


def encrypt(data, password):
    salt, nonce = os.urandom(16), os.urandom(12)
    header = MAGIC + salt + nonce
    return header + AESGCM(key(password, salt)).encrypt(nonce, data, header)


def decrypt(data, password):
    if not data.startswith(MAGIC) or len(data) < 51:
        raise ValueError("Invalid snapshot")
    header = data[:35]
    return AESGCM(key(password, data[7:23])).decrypt(data[23:35], data[35:], header)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["backup", "restore"])
    parser.add_argument("--database", default="convertbank")
    parser.add_argument("--host", default="localhost")
    parser.add_argument("--port", default="5432")
    parser.add_argument("--user", default="postgres")
    parser.add_argument("--pg-bin", type=Path)
    parser.add_argument("--uploads", type=Path, default=ROOT / "uploads")
    parser.add_argument("--file", type=Path, default=ROOT / "data/transfer/snapshot.enc")
    args = parser.parse_args()
    if not re.fullmatch(r"[a-z][a-z0-9_]{0,62}", args.database):
        parser.error("Use a lowercase database name containing letters, numbers and underscores")
    pg_bin = args.pg_bin
    if not pg_bin and os.name == "nt":
        pg_bin = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "PostgreSQL/17/bin"

    def run(tool, *extra, database=None):
        executable = str(pg_bin / tool) if pg_bin else tool
        command = [executable, "-h", args.host, "-p", args.port, "-U", args.user, "-w"]
        if database:
            command += ["-d", database]
        return subprocess.run(command + list(extra), env=env, check=True, capture_output=True, text=True).stdout

    password = getpass.getpass("Snapshot password (keep outside GitHub): ")
    if args.action == "backup":
        if len(password) < 16:
            parser.error("Snapshot password must contain at least 16 characters")
        if password != getpass.getpass("Confirm snapshot password: "):
            parser.error("Passwords do not match")
    env = os.environ.copy()
    env["PGPASSWORD"] = getpass.getpass("PostgreSQL password: ")
    with tempfile.TemporaryDirectory(prefix="releveflow-") as temporary:
        temp = Path(temporary)
        if args.action == "backup":
            if not args.uploads.is_dir():
                parser.error("Uploads directory missing; specify --uploads")
            run("pg_dump", "-Fc", "--file", str(temp / "database.dump"), database=args.database)
            with zipfile.ZipFile(temp / "snapshot.zip", "w", zipfile.ZIP_DEFLATED) as archive:
                archive.write(temp / "database.dump", "database.dump")
                for path in sorted(args.uploads.rglob("*")):
                    if path.is_symlink():
                        raise ValueError("Symlinks are not supported in uploads")
                    if path.is_file():
                        archive.write(path, "uploads/" + path.relative_to(args.uploads).as_posix())
            payload = encrypt((temp / "snapshot.zip").read_bytes(), password)
            # Stay below GitHub's ordinary per-file limit, with room for metadata.
            if len(payload) > 95 * 1024 * 1024:
                raise ValueError("Snapshot exceeds 95 MiB; use private external backup storage")
            args.file.parent.mkdir(parents=True, exist_ok=True)
            staged = args.file.with_suffix(".tmp")
            staged.write_bytes(payload)
            staged.replace(args.file)
            print("Encrypted backup created:", args.file)
        else:
            payload = decrypt(args.file.read_bytes(), password)
            with zipfile.ZipFile(io.BytesIO(payload)) as archive:
                for entry in archive.infolist():
                    target = (temp / entry.filename).resolve()
                    if not target.is_relative_to(temp.resolve()):
                        raise ValueError("Unsafe archive path")
                    if entry.filename != "database.dump" and not entry.filename.startswith("uploads/"):
                        raise ValueError("Unexpected archive entry")
                archive.extractall(temp)
            if not (temp / "database.dump").is_file():
                raise ValueError("Database dump missing")
            exists = run("psql", "-At", "-v", "ON_ERROR_STOP=1", "-c",
                         f"SELECT 1 FROM pg_database WHERE datname = '{args.database}'", database="postgres").strip()
            if exists:
                raise ValueError("Database already exists. Use --database with a NEW name; nothing was overwritten")
            if args.uploads.exists() and any(args.uploads.iterdir()):
                raise ValueError("Uploads directory is not empty. Choose a NEW --uploads directory")
            run("createdb", args.database)
            try:
                run("pg_restore", "--no-owner", "--no-acl", "--exit-on-error", "--single-transaction",
                    str(temp / "database.dump"), database=args.database)
                args.uploads.mkdir(parents=True, exist_ok=True)
                if (temp / "uploads").exists():
                    shutil.copytree(temp / "uploads", args.uploads, dirs_exist_ok=True)
            except Exception:
                print("Restore incomplete. New database retained for inspection:", args.database)
                raise
            print("Restored database:", args.database)
            print("Documents:", args.uploads.resolve())
            print("Configure ConnectionStrings__Default and Storage__Root locally before starting the API.")


if __name__ == "__main__":
    try:
        main()
    except subprocess.CalledProcessError as error:
        raise SystemExit(error.stderr.strip())
    except Exception as error:
        raise SystemExit(f"Operation failed ({type(error).__name__}): {error}")
