#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")"
command -v docker >/dev/null || { echo 'Installer Docker Engine et Docker Compose sur ce serveur Linux.' >&2; exit 1; }
docker compose version >/dev/null
command -v curl >/dev/null || { echo 'Installer curl avant de continuer.' >&2; exit 1; }
umask 077
if [[ ! -f .env ]]; then
  read -r -p 'Port frontend [3002] : ' front
  read -r -p 'Port backend local [5081] : ' back
  read -r -p 'Nom DNS ou IP fixe du serveur : ' host
  front=${front:-3002}; back=${back:-5081}
  for port in "$front" "$back"; do
    [[ $port =~ ^[1-9][0-9]{3,4}$ ]] && (( port >= 1024 && port <= 65535 )) || { echo 'Port invalide.' >&2; exit 1; }
    if command -v ss >/dev/null && [[ -n $(ss -H -ltn "sport = :$port") ]]; then echo "Port $port occupe." >&2; exit 1; fi
  done
  [[ $front != "$back" ]] || { echo 'Les deux ports doivent etre distincts.' >&2; exit 1; }
  [[ $host =~ ^[A-Za-z0-9][A-Za-z0-9.-]*$ ]] || { echo 'Nom ou IPv4 invalide.' >&2; exit 1; }
  # Hexadecimal password avoids shell, dotenv and connection-string interpolation.
  password=$(od -An -N32 -tx1 /dev/urandom | tr -d ' \n')
  printf 'FRONTEND_PORT=%s\nBACKEND_PORT=%s\nPUBLIC_HOST=%s\nDB_PASSWORD=%s\n' "$front" "$back" "$host" "$password" > .env
  unset password
else
  echo 'Configuration existante conservee. Les volumes de donnees seront reutilises.'
fi
docker compose --env-file .env -f compose.linux.yml up -d --build
front=$(sed -n 's/^FRONTEND_PORT=//p' .env)
host=$(sed -n 's/^PUBLIC_HOST=//p' .env)
ready=false
for ((attempt=0; attempt<90; attempt++)); do
  if curl --fail --silent --max-time 3 "http://127.0.0.1:$front/api/auth/session" | grep -q 'setupRequired'; then ready=true; break; fi
  sleep 2
done
[[ $ready == true ]] || { echo 'Application indisponible : consulter docker compose --env-file .env -f compose.linux.yml logs.' >&2; exit 1; }
printf '[InternetShortcut]\r\nURL=http://%s:%s\r\n' "$host" "$front" > Acces-Alias.url
echo "Alias disponible : http://$host:$front"
echo "Autoriser le port TCP $front depuis le reseau des postes dans le pare-feu du serveur."
echo 'Distribuer Acces-Alias.url aux postes. Aucun logiciel Alias a installer sur les postes.'
