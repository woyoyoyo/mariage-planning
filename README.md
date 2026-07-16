# Mariage Planning 💍

Coordinateur du mariage — timeline de la semaine (J-2 à J+1), qui fait quoi, tâches multi-personnes.

PWA Blazor WebAssembly (.NET 10), même architecture que [RoadTrip QC](https://github.com/woyoyoyo/roadtrip-qc) :
les données vivent dans un **Gist GitHub** (`mariage.json`), l'app est déployée sur **GitHub Pages**.

## Pages

- `/` — timeline complète (tous les jours, toutes les tâches), Jour J mis en avant ✨
- `/moi` — mon planning (choix de la personne mémorisé sur le téléphone)
- `/personne/:id` — planning filtré d'une personne, partage WhatsApp, export PDF
- `/parametres` — ID du Gist + token GitHub (PAT), stockés en localStorage uniquement

## Fonctionnement

- **Lecture** : API GitHub (Gist) → cache localStorage → données de démo
- **Écriture** : fetch-avant-PATCH — on relit toujours la dernière version du Gist avant
  d'appliquer une modification, pour ne pas écraser les changements d'un autre téléphone
- **Tap sur une tâche** = bascule todo ✅ / à faire ⬜

## Modèle de données (`mariage.json`)

```json
{
  "wedding": { "name": "Mariage Yoann & Léa", "date": "2027-06-12" },
  "people": [
    { "id": "yoann", "name": "Yoann", "color": "#6366f1" }
  ],
  "days": [
    {
      "date": "2027-06-10",
      "label": "J-2 — Préparation",
      "activities": [
        {
          "id": "t-xxx",
          "time": "16:00",
          "title": "Récupération fleurs + dépôt salle",
          "assignedTo": ["yoann"],
          "notes": "Camionnette de Bernard",
          "status": "todo"
        }
      ]
    }
  ]
}
```

`assignedTo` vide = tout le monde. `time` optionnel (tâche "toute la journée").

## Développement

```bash
dotnet run --project MariagePlanning
```

## Déploiement

Push sur `main` → GitHub Actions publie sur GitHub Pages (`/mariage-planning/`).
