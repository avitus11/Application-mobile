# Quiz Party

Application de quiz à jouer entre amis, dans la même pièce. L'écran animateur (PC branché sur la TV) affiche les questions, et les joueurs répondent depuis leur téléphone, connectés au Wi-Fi de la maison. Aucun compte, aucun cookie, pas besoin d'internet.

## V1 : ce qui est prêt

- Création de la partie avec un code à 4 lettres et un QR code
- Les joueurs rejoignent avec un pseudo (refusé s'il est déjà pris) et se reconnectent tout seuls si le téléphone se met en veille
- QCM : question et 4 réponses à l'écran, réponse A/B/C/D sur le téléphone, chrono lancé à la main, réponses bloquées à la fin du temps
- 1 point par bonne réponse, sans bonus de vitesse ; les ex æquo partagent le même rang
- Scores calculés en direct mais masqués jusqu'au clic sur « Afficher les scores »

Prévu pour la V2 : duos, Synonymes, épreuves d'élimination, tableau final, sons.

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
3. **Lancer le QCM** → **Lancer le chrono** → **Révéler la réponse** → **Question suivante**.
4. **Afficher les scores** montre le classement sur la TV et sur les téléphones, au moment que tu choisis.

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
      "answer": 2,      // 0 = A, 1 = B, 2 = C, 3 = D
      "duration": 15    // secondes de chrono (20 si absent)
    }
  ]
}
```

Le fichier est relu à chaque **Nouvelle partie** : pas besoin de redémarrer le serveur. S'il contient une erreur, l'écran animateur affiche le numéro de la question en cause.

## Réglages (`server/QuizParty.Server/appsettings.json`)

| Clé | Rôle |
| --- | --- |
| `Quiz:Port` | Port du serveur (5000 par défaut) |
| `Quiz:QcmPath` | Chemin du fichier de questions |
| `Quiz:PublicUrl` | À renseigner (ex. `http://192.168.1.20:5000`) si le QR code affiche une mauvaise adresse IP |
| `Quiz:AllowRemoteHost` | `true` pour ouvrir l'écran animateur depuis un autre appareil |

## Développement

```bash
dotnet run --project server/QuizParty.Server   # serveur sur :5000
cd client && npm start                          # Angular sur :4200, /hub redirigé vers :5000
dotnet test                                     # tests du serveur
```

Organisation :

- `server/QuizParty.Server` : ASP.NET Core + SignalR. La logique du jeu est dans `Game/GameState.cs` (sans réseau, testable) ; `GameService` gère le chrono et la diffusion, et `Hubs/QuizHub` sert de point d'entrée temps réel.
- `server/QuizParty.Tests` : tests xUnit (inscription, points, classement, validation du JSON).
- `client` : Angular (signals), avec trois pages : `/` accueil, `/host` animateur, `/play` joueur.
- `content` : les questions.
