# Backend FastAPI

## Rôle

Cette API reçoit :

- `environment` : PNG ou JPEG dont l'utilisateur possède les droits ;
- `mask` : PNG noir et blanc de mêmes dimensions ;
- `manifest_json` : buts, dimensions, limites et cosmétiques ;
- `prompt` : instruction courte d'inpainting.

Le blanc autorise la modification. Le noir protège l'image originale.

Le fichier `vision_api.py` est le second service du PoC. Il transforme une capture de rue autorisée en JSON de biome compris par Unity. Son mode `demo` est activé par défaut : aucune clé et aucun appel externe ne sont nécessaires.

## Installation locale sans GPU

```bash
python -m venv .venv
source .venv/bin/activate
pip install -e ".[dev]"
uvicorn app.main:app --reload
```

Ouvrez ensuite `http://localhost:8000/docs`.

## Démarrer l'analyseur Vision autonome

```bash
uvicorn vision_api:app --reload --port 8001
```

Le JSON simulé est disponible à l'adresse :

```text
http://localhost:8001/v1/vision/demo-biome
```

Pour activer l'analyse OpenAI sur le serveur uniquement :

```bash
export STREET_TURF_VISION_MODE=openai
export STREET_TURF_VISION_MODEL=gpt-5.6
export OPENAI_API_KEY=remplacez_par_votre_cle_serveur
uvicorn vision_api:app --host 0.0.0.0 --port 8001
```

La clé OpenAI ne doit jamais être intégrée dans l'application Unity ou dans GitHub. En production, placez-la dans le gestionnaire de secrets du serveur.

Pour tester depuis un téléphone sur le même réseau, remplacez `192.168.1.50` par l'adresse locale de l'ordinateur, puis démarrez ainsi :

```bash
export STREET_TURF_PUBLIC_BASE_URL=http://192.168.1.50:8000
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

## Test direct de l'inpainting

```bash
curl -X POST http://localhost:8000/v1/inpaint \
  -F "image=@original.png;type=image/png" \
  -F "mask=@mask.png;type=image/png" \
  -F "prompt=empty urban asphalt and wall, realistic continuation" \
  -F "seed=42" \
  --output cleaned.png
```

Avec le fournisseur `mock`, le résultat est un remplissage flouté utile seulement pour tester la chaîne réseau. Il ne prétend pas être un rendu final.

## Création d'un Home Turf

Créez un fichier `draft.json` contenant exactement :

```json
{
  "schemaVersion": 1,
  "turfName": "Rue des Champions",
  "sourceKind": "user_capture",
  "sourceReference": "capture-mobile-2026-07-18",
  "lengthMeters": 24.0,
  "widthMeters": 12.0,
  "goals": [
    {
      "position": { "x": 0.0, "y": 0.0, "z": -12.0 },
      "rotation": { "x": 0.0, "y": 0.0, "z": 0.0, "w": 1.0 }
    },
    {
      "position": { "x": 0.0, "y": 0.0, "z": 12.0 },
      "rotation": { "x": 0.0, "y": 1.0, "z": 0.0, "w": 0.0 }
    }
  ],
  "boundaries": [
    { "x": -6.0, "y": 0.0, "z": -12.0 },
    { "x": 6.0, "y": 0.0, "z": -12.0 },
    { "x": 6.0, "y": 0.0, "z": 12.0 },
    { "x": -6.0, "y": 0.0, "z": 12.0 }
  ],
  "cosmetics": {
    "graffitiSku": "graffiti_free_01",
    "goalNetSku": "net_classic_white",
    "weatherSku": "weather_day_clear"
  }
}
```

Envoyez ensuite :

```bash
curl -X POST http://localhost:8000/v1/home-turfs \
  -F "environment=@original.png;type=image/png" \
  -F "mask=@mask.png;type=image/png" \
  -F "manifest_json=<draft.json" \
  -F "prompt=empty urban asphalt and wall, realistic continuation"
```

La réponse JSON est le manifeste officiel que le mobile Home conserve et que le mobile Away téléchargera avant le match.

## Activer Diffusers

Une carte NVIDIA disposant d'environ 8 Go de VRAM est une base raisonnable pour ce POC. La mémoire dépend du modèle et de la taille du recadrage.

```bash
pip install -e ".[ai,dev]"
export STREET_TURF_INPAINT_PROVIDER=diffusers
export STREET_TURF_DEVICE=cuda
export STREET_TURF_TORCH_DTYPE=float16
uvicorn app.main:app --host 0.0.0.0 --port 8000
```

Le service recadre autour du masque avant l'appel au modèle, puis redimensionne ce recadrage au maximum configuré. Enfin, il recolle le résultat avec le masque original. Ainsi, les pixels non sélectionnés sont conservés exactement.

## Tests

```bash
python -m unittest discover -s tests -p "test_*.py"
```

Après installation des dépendances de développement :

```bash
pytest
```

## Ce qui doit changer avant la production

- authentification OAuth/JWT ;
- stockage S3/GCS compatible et CDN ;
- PostgreSQL ;
- file Redis/SQS/PubSub et workers GPU séparés ;
- vérification antivirus, modération et floutage de données personnelles ;
- URL signées, quotas et limitation de débit ;
- suppression automatique de l'image originale ;
- journal de provenance et consentement ;
- tests de charge et métriques GPU.
