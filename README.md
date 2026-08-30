# Portail de Dépôt et de Validation de la Liasse Fiscale

Portail officiel pour le téléversement, la validation réglementaire XSD multi-niveaux, le dépôt officiel, l'horodatage certifié et le suivi de la Liasse Fiscale (Ministère des Finances - Direction Générale des Impôts).

---

## 1. Technologies Utilisées

Ce projet s'appuie sur un écosystème moderne, résilient et performant :

### Backend & Moteur Fiscale
- **.NET 10 (ASP.NET Core Web API / C#)** : Architecture REST modulaire haute performance, injection de dépendances, middleware d'authentification et gestion de flux asynchrones.
- **Entity Framework Core 10 (EF Core)** : ORM pour la modélisation des entités fiscales, le suivi des états et les migrations automatiques.
- **System.Xml & XPath Engine** : Validation multi-couches stricte contre les schémas XSD 1.0 DGI et évaluation des formules comptables dynamiques (`AssertRuleEngine`).
- **Swagger / OpenAPI** : Documentation interactive et typée des endpoints REST.
- **JWT (JSON Web Tokens)** : Sécurisation des sessions déclarants et gestion de l'autorisation à granularité fine.

### Base de Données & Persistance
- **PostgreSQL 17 / 16** : Système de gestion de base de données relationnelle assurant l'intégrité transactionnelle (ACID), le stockage des liasses, l'audit trail et l'historique complet des télé-déclarations.
- **JSONB / Schemas Postgres** : Stockage flexible des métadonnées XML, logs d'anomalies de validation et configurations d'états financiers.

### Frontend & Interface Utilisateur
- **TypeScript & ECMAScript 2024** : Logique applicative typée et réactive côté client.
- **HTML5 & CSS3 / Tailwind Engine** : Interface conforme aux chartes graphiques administratives de la DGI (ergonomie soignée, responsive et accessible).
- **Mappage XML vers HTML / XSLT Visualizer** : Restitution instantanée des bilans, comptes de résultat et tableaux fiscaux au format tabulaire officiel.
- **Accusés de Réception & Empreintes SHA-256** : Génération et horodatage des reçus de dépôt avec QR Code de vérification.

### DevOps, Tests & Qualité
- **Docker & Docker Compose** : Conteneurisation complète (API, Frontend, Base PostgreSQL).
- **xUnit / NSubstitute / FluentAssertions** : Suite automatisée de tests unitaires et d'intégration validant le moteur XML et les règles métier.

---

## 2. Architecture du Système

Le système repose sur un **backend centralisé en .NET 10** garantissant l'intégrité absolue de la validation fiscale et des règles comptables.

```text
Frontend TypeScript / Web Client (Port 3000)
             |
             | HTTP / REST (JSON + multipart/form-data)
             v
   .NET 10 Web API (LiasseFiscale.Api)
             |
             +--> Controllers (Auth, Contribuable, Liasse, Document, Validation, Tracking, Receipt)
             |
             +--> Services
             |      +--> XmlValidationService (Validation XSD 1.0 + Racine XML)
             |      +--> AssertRuleEngine (Évaluation XPath des règles d'équilibre comptable)
             |      +--> ReceiptService (Génération de l'accusé de réception officiel PDF/SHA256)
             |
             +--> SchemaAssets/
             |      +--> structural/ (XSD 1.0 par état financier : F6001, F6002, ...)
             |      +--> rules/ (JSON des formules d'assertions comptables)
             |
             +--> Entity Framework Core 10
             |
             v
        PostgreSQL (Persistance sécurisée, Liasses, Dépôts & Audit trail)
```

---

## 3. Pipeline de Validation XML Multi-Niveaux

La validation d'un état financier téléversé s'effectue selon 5 niveaux de contrôle obligatoires :

1. **Niveau 1 — Code Document & Extension** :
   - Contrôle du code attendu (ex. `F6001`, `F6002`, `F6003`, `F6004`, `F6005`, `F6007`, `F6019`, `F6201`...).
   - Extension obligatoire `.xml` (ou `.pdf` pour le `F6019`).

2. **Niveau 2 — Masque du nom de fichier** :
   - Vérification du format normalisé : `[CodeDocument]-[MatriculeFiscalDeclarant]-[Exercice].[ext]`
   - Exemple : `F6001-1234567M-2026.xml`.

3. **Niveau 3 — Racine XML & Espace de Noms** :
   - L'élément racine XML doit strictement correspondre au document attendu (ex. `<lf:F6001 ...>`).
   - L'espace de noms officiel doit être `http://www.impots.finances.gov.tn/liasse`.
   - *Tout document avec une racine divergente est immédiatement rejeté avec statut `Invalide`.*

4. **Niveau 4 — Validation Structurelle XSD 1.0** :
   - Validation formelle via `XmlSchemaSet` et `XmlReader`.
   - Détection précise des éléments manquants, inattendus, types de données incorrects ou contraintes d'énumérations violées.
   - *Une seule erreur XSD invalide immédiatement le document (`IsValid = false`).*

5. **Niveau 5 — Moteur de Règles Métier (`AssertRuleEngine`)** :
   - Évaluation XPath des équations comptables et sommes d'agrégation (ex. Total Actif = Total Passif, Total Produits = Somme des rubriques).
   - Les violations sont classées sous la source `RegleMetier`.

---

## 4. Principaux Points d'Entrée de l'API

L'ensemble des requêtes transite par l'API REST .NET 10 :

| Méthode | Route API | Description |
| :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Authentification JWT et initialisation de session |
| `GET` | `/api/contribuables/{matricule}` | Consultation de la fiche d'un contribuable |
| `POST` | `/api/liasses` | Création / initialisation d'une liasse fiscale pour un exercice |
| `GET` | `/api/liasses/{id}` | Détails d'une liasse et état des documents associés |
| `POST` | `/api/liasses/{id}/documents/{code}` | **Téléversement et validation d'un état financier** |
| `DELETE`| `/api/liasses/{id}/documents/{code}` | Détachement / suppression d'un fichier téléversé |
| `GET` | `/api/liasses/{id}/documents/{code}/download` | Téléchargement du fichier XML / PDF original |
| `GET` | `/api/liasses/{id}/documents/{code}/html` | Visualisation tabulaire et impression de l'état financier |
| `POST` | `/api/validation/{codeDocument}` | **Validation à blanc** d'un fichier XML sans l'enregistrer |
| `POST` | `/api/deposits` | **Dépôt officiel** de la liasse (si tous les documents obligatoires sont valides) |
| `GET` | `/api/tracking/{reference}` | Suivi en temps réel de l'état d'un dépôt officiel |
| `GET` | `/api/tracking/{reference}/receipt/pdf` | Téléchargement de l'Accusé de Réception officiel (PDF / Empreinte SHA256) |

---

## 5. Démarrage du Projet

### Prérequis
- **.NET SDK 10.0**
- **Node.js 20+**
- **PostgreSQL 16 / 17** (ou instance Docker)

### A. Démarrer le Backend .NET 10
```bash
cd LiasseFiscale.Api
dotnet restore
dotnet ef database update
dotnet run
```
*L'API écoute par défaut sur `http://localhost:5000` (ou `https://localhost:5001`).*
*Swagger UI accessible en mode développement sur `/swagger`.*

### B. Démarrer le Frontend / Application Web
```bash
npm install
npm run dev
```
*Le portail est accessible sur `http://localhost:3000`.*

### C. Démarrage complet via Docker Compose
```bash
docker compose up --build
```

---

## 6. Exécution des Tests Automatisés

La suite de tests unitaires et d'intégration couvre l'intégralité du moteur de validation :

```bash
cd LiasseFiscale.Tests
dotnet test
```

### Scénarios de tests validés :
- ✔ **F6001 Conforme** : Validation structurelle et métier réussie (`IsValid = true`).
- ✔ **F6002 envoyé pour F6001** : Rejet immédiat sur discordance de nom et de racine (`IsValid = false`).
- ✔ **Racine XML erronée** : Détection de l'élément racine inattendu (`Source = Structurelle`).
- ✔ **XML non conforme au schéma XSD** : Capture des champs manquants ou interdits.
- ✔ **Règle métier arithmétique violée** : Détection des erreurs de calcul XPath (`Source = RegleMetier`).

---

## 7. Prétraitement des Schémas et Règles DGI

En cas de mise à jour des schémas XSD officiels par la Direction Générale des Impôts :

1. Placer les nouveaux fichiers dans `LiasseFiscale.Api/SchemaAssets/original/`.
2. Exécuter l'outil de prétraitement :
   ```bash
   cd Tools/SchemaPreprocessor
   dotnet run -- ../../LiasseFiscale.Api/SchemaAssets/original ../../LiasseFiscale.Api/SchemaAssets/structural ../../LiasseFiscale.Api/SchemaAssets/rules
   ```
3. Committer les schémas structurels et règles extraites mis à jour.
