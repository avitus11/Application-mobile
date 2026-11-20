# Quiz Party

Application de quiz à jouer entre amis, dans la même pièce. L'écran animateur (PC branché sur la TV) affiche les questions, et les joueurs répondent depuis leur téléphone, connectés au Wi-Fi de la maison. Aucun compte, aucun cookie, pas besoin d'internet.

## Ce qui est prêt

- Création de la partie avec un code à 4 lettres et un QR code
- Les joueurs rejoignent avec un pseudo (refusé s'il est déjà pris) et se reconnectent tout seuls si le téléphone se met en veille
- QCM : question et 4 réponses à l'écran, réponse A/B/C/D sur le téléphone, chrono de 15 s lancé à la main, réponses bloquées à la fin du temps
- 1 point par bonne réponse, sans bonus de vitesse ; les ex æquo partagent le même rang
- Scores calculés en direct mais masqués jusqu'au clic sur « Afficher les scores »
- **Duos** formés à partir du classement du QCM : 1er + dernier, 2e + avant-dernier… Avec un nombre impair de joueurs, celui du milieu rejoint le duo du 1er (trio). Les ex æquo sont départagés au hasard.
- **Synonymes** : l'animateur choisit l'équipe qui joue. Le mot s'affiche uniquement sur le téléphone de celui qui fait deviner, avec un chrono de 45 s. Chaque membre de l'équipe fait deviner une fois. Mot trouvé = +1 point pour chaque membre ; mot passé = sans pénalité.

À venir : épreuves d'élimination, tableau final, sons.

## Prérequis

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org) 22 ou plus

## Lancer une partie

```powershell
./start.ps1        # Windows
./start.sh         # macOS / Linux
```

Le script compile l'appli Angular puis démarre le serveur. La console affiche :

```
Écran animateur (sur ce PC) : http://localhost:5000/host
Joueurs (même Wi-Fi)         : http://192.168.x.x:5000/play
```

1. Ouvre `http://localhost:5000/host` sur le PC et clique sur **Créer la partie**.
2. Les joueurs scannent le QR code (ou tapent l'adresse et le code) puis choisissent un pseudo.
3. **Lancer le QCM** → **Lancer le chrono** (les réponses se bloquent à la fin des 15 s) → **Révéler la réponse** → **Question suivante**.
4. **Terminer le QCM** → **Former les duos** → **Passer aux Synonymes**.
5. Clique sur une équipe → **C'est parti !**. « Trouvé ✓ » et « Passer » se font depuis le téléphone de celui qui fait deviner ou depuis ton écran. Pour vérifier un mot sans le montrer à la salle, garde le doigt sur « Maintenir pour voir le mot ».
6. **Afficher les scores** montre le classement sur la TV et sur les téléphones, au moment que tu choisis.

> **Pare-feu Windows** : au premier lancement, Windows demande d'autoriser `QuizParty.Server`. Accepte pour les **réseaux privés**, et vérifie que le Wi-Fi de la maison est bien déclaré comme réseau privé. Sans ça, les téléphones ne peuvent pas se connecter.

L'écran animateur ne s'ouvre que sur le PC qui fait tourner le serveur : un joueur ne peut pas piloter la partie depuis son téléphone.

## Préparer les questions

Les questions sont dans [`content/qcm.json`](content/qcm.json) :

```jsonc
{
  "title": "QCM - Culture générale",
  "questions": [
    {
      "text": "Quelle est la capitale de l'Australie ?",
      "choices": ["Sydney", "Melbourne", "Canberra", "Perth"],
      "answer": 2       // 0 = A, 1 = B, 2 = C, 3 = D
    }
  ]
}
```

Les mots des Synonymes sont dans [`content/synonymes.json`](content/synonymes.json) (`"words": [...]`). Chaque mot ne sort qu'une fois par partie, dans un ordre tiré au hasard. Prévois large : 15 tours de 45 s peuvent consommer plus de 100 mots.

Les fichiers sont relus à chaque **Nouvelle partie** : pas besoin de redémarrer le serveur. S'il contient une erreur, l'écran animateur affiche le numéro de la question en cause.

## Réglages (`server/QuizParty.Server/appsettings.json`)

| Clé | Rôle |
| --- | --- |
| `Quiz:Port` | Port du serveur (5000 par défaut) |
| `Quiz:QcmPath` | Chemin du fichier de questions |
| `Quiz:TimerSeconds` | Durée du chrono, la même pour toutes les questions (15 par défaut) |
| `Quiz:SynonymsPath` | Chemin du fichier de mots |
| `Quiz:SynonymsSeconds` | Durée d'un tour de Synonymes (45 par défaut) |
| `Quiz:PublicUrl` | À renseigner (ex. `http://192.168.1.20:5000`) si le QR code affiche une mauvaise adresse IP |
| `Quiz:AllowRemoteHost` | `true` pour ouvrir l'écran animateur depuis un autre appareil |

## Développement

```bash
dotnet run --project server/QuizParty.Server   # serveur sur :5000
cd client && npm start                          # Angular sur :4200, /hub redirigé vers :5000
dotnet test                                     # tests du serveur
```

Organisation :

- `server/QuizParty.Server` : ASP.NET Core + SignalR. La logique du jeu est dans `Game/GameState.cs` et `Game/GameState.Synonyms.cs` (sans réseau, testable) ; `GameService` gère le chrono et la diffusion, et `Hubs/QuizHub` sert de point d'entrée temps réel.
- `server/QuizParty.Tests` : tests xUnit (inscription, points, classement, duos, Synonymes, validation du JSON).
- `client` : Angular (signals), avec trois pages : `/` accueil, `/host` animateur, `/play` joueur.
- `content` : les questions.
