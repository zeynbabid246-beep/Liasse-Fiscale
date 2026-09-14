# 🏛️ Portail Officiel de Dépôt et de Suivi de la Liasse Fiscale

[![TypeScript](https://img.shields.io/badge/TypeScript-5.7-blue.svg)](https://www.typescriptlang.org/)
[![Node.js](https://img.shields.io/badge/Node.js-20+-green.svg)](https://nodejs.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-14+-336791.svg)](https://www.postgresql.org/)
[![Express](https://img.shields.io/badge/Express-4.21-lightgrey.svg)](https://expressjs.com/)
[![XML/XSD Engine](https://img.shields.io/badge/XML%20XSD-1.0%20Compliant-orange.svg)](#2-pipeline-de-validation-xml-multi-niveaux)

Portail officiel pour le téléversement, la validation réglementaire XML / XSD multi-niveaux, le dépôt sécurisé, l'horodatage certifié et le suivi de la **Liasse Fiscale** (Ministère des Finances - Direction Générale des Impôts).

---

## 📑 Table des Matières

1. [Architecture & Technologies](#1-architecture--technologies)
2. [Base de Données (PostgreSQL)](#2-base-de-données-postgresql)
3. [Pipeline de Validation XML Multi-Niveaux](#3-pipeline-de-validation-xml-multi-niveaux)
4. [Configuration de l'Environnement & Fichier `.env`](#4-configuration-de-lenvironnement--fichier-env)
5. [Guide d'Installation & Démarrage](#5-guide-dinstallation--démarrage)
6. [Points d'Entrée de l'API](#6-points-dentrée-de-lapi)
7. [Comptes de Test & Fichiers d'Exemple](#7-comptes-de-test--fichiers-dexemple)
8. [Scripts NPM Disponibles](#8-scripts-npm-disponibles)

---

## 1. Architecture & Technologies

L'application repose sur une architecture moderne et autonome, propulsée par un backend unifié et performant en **TypeScript / Node.js** :

### Backend & Moteur de Validation Fiscale (`server.ts`)
- **Node.js & Express (TypeScript)** : Serveur API REST unifié assurant l'authentification JWT, la gestion du cycle de vie des liasses fiscales, les téléversements multi-fichiers et la génération des accusés officiels.
- **Moteur de Validation XML Multi-Niveaux** :
  - **`xml-xsd-engine` & `fast-xml-parser`** : Moteur de conformité strict basé sur les schémas XSD 1.0 officiels de la Direction Générale des Impôts (`/SchemaAssets/original/`).
  - **Moteur d'Assertions & Règles Métier** : Contrôle arithmétique des équilibres comptables (Actif / Passif, décomposition du résultat, flux de trésorerie).
- **Sécurité & Sessions** : Tokens JWT (`jsonwebtoken`) avec gestion des profils Déclarant (Contribuable) et Administration Fiscale (DGI).
- **Génération & Horodatage Certifié** : Calcul d'empreintes numériques **SHA-256**, génération d'accusés de réception et restitution tabulaire HTML imprimable.

### Frontend & Interface Utilisateur (`/public`)
- **Single Page Application (SPA)** : Interface réactive respectant la charte graphique officielle du Ministère des Finances.
- **Restitution Tabulaire & Mappage XML $\rightarrow$ HTML** : Consultation en direct du contenu financier structuré.
- **Visualisation des Anomalies** : Détection granulaire des erreurs (source, balise XML incriminée, ligne et message explicatif).

---

## 2. Base de Données (PostgreSQL)

L'application utilise **PostgreSQL** (version 14, 15, 16 ou supérieure) pour la persistance relationnelle, l'intégrité référentielle et l'audit des opérations.

### Architecture à Double Mode (Dual-Mode)
- **Mode PostgreSQL (Recommandé)** : Lorsque la variable `DATABASE_URL` est renseignée dans le fichier `.env`, l'application se connecte au serveur PostgreSQL, vérifie la présence du schéma et synchronise toutes les données dans les tables relationnelles.
- **Mode Mémoire / Fichiers Locaux (Fallback)** : Si aucune variable `DATABASE_URL` n'est détectée ou si la base est temporairement inaccessible, l'application continue de fonctionner sans interruption en stockant les états en mémoire vive et sur le système de fichiers local (`/uploads`).

### Modèle Relationnel (`src/db/schema.sql`)

| Table | Description |
| :--- | :--- |
| `users` | Comptes utilisateurs, rôles (`DECLARANT` ou `ADMIN`), matricule fiscal et régime fiscal |
| `deposits` | Dossiers de dépôt des liasses, exercices, références officielles et statuts de traitement |
| `deposit_files` | Fichiers téléversés (XML / PDF), empreintes de hachage SHA-256, tailles et rapports de validation JSONB |
| `declaration_details` | Soldes et valeurs déclarées par rubrique comptable (extrait des formulaires XML) |
| `audit_logs` | Journal d'audit horodaté pour la traçabilité des opérations réglementaires |

---

## 3. Pipeline de Validation XML Multi-Niveaux

La validation d'un état financier téléversé s'effectue selon **5 niveaux de contrôle réglementaires** :

1. **Niveau 1 — Code Document & Extension** :
   - Contrôle du code attendu (ex. `F6001`, `F6002`, `F6003`, `F6004`, `F6005`, `F6007`, `F6019`, `F6201`...).
   - Extension obligatoire `.xml` (ou `.pdf` pour les annexes `F6019`).

2. **Niveau 2 — Nomenclature du Nom de Fichier** :
   - Masque normalisé : `[CodeDocument]-[MatriculeFiscal]-[Exercice].[ext]`
   - Exemple : `F6001-0000121J-2026.xml`.

3. **Niveau 3 — Racine XML & Espace de Noms** :
   - L'élément racine XML doit correspondre au document attendu (`<F6001>`, `<F6002>`, etc.).
   - Namespace officiel : `http://www.impots.finances.gov.tn/liasse`.

4. **Niveau 4 — Validation Structurelle XSD 1.0** :
   - Validation formelle par rapport aux schémas officiels de la DGI (`/SchemaAssets/original/`).
   - Vérification des types de données, contraintes d'énumérations et éléments obligatoires de l'entête (`T_Entete`).

5. **Niveau 5 — Moteur de Règles Arithmétiques & Équilibres** :
   - Évaluation des formules d'agrégation comptable (Total Actif = Total Passif, Total Produits - Total Charges = Résultat Net).

---

## 4. Configuration de l'Environnement & Fichier `.env`

Pour faciliter le déploiement et la configuration locale, le projet inclut un fichier modèle **`.env.example`**.

### 1. Création du fichier `.env`

Copiez le fichier d'exemple pour créer votre fichier `.env` local :

**Sous Windows (PowerShell) :**
```powershell
Copy-Item .env.example .env
```

**Sous Windows (CMD) :**
```cmd
copy .env.example .env
```

**Sous Linux / macOS :**
```bash
cp .env.example .env
```

### 2. Contenu et Paramètres du fichier `.env`

Voici le contenu type attendu dans le fichier `.env` :

```env
# ==============================================================================
# Portail Liasse Fiscale - Variables d'environnement
# ==============================================================================

# Port d'écoute du serveur HTTP (défaut : 3000)
PORT=3000

# Environnement d'exécution (development | production)
NODE_ENV=development

# Clé secrète utilisée pour signer et vérifier les jetons JWT
JWT_SECRET=LiasseFiscaleSecretKey2026_DGI_SuperSecureKey_MinFinances

# Chaîne de connexion PostgreSQL (optionnelle)
# Format : postgresql://<utilisateur>:<mot_de_passe>@<hôte>:<port>/<base_de_données>
DATABASE_URL=postgresql://postgres:postgres@localhost:5432/liasse_fiscale_db
```

### Description détaillée des variables

| Variable | Obligatoire | Valeur par défaut | Description |
| :--- | :---: | :--- | :--- |
| `PORT` | Non | `3000` | Port TCP sur lequel le serveur Express écoute les requêtes. |
| `NODE_ENV` | Non | `development` | Mode d'exécution (`development` ou `production`). |
| `JWT_SECRET` | Non | Clé interne intégrée | Clé privée pour la génération des jetons d'authentification JWT. |
| `DATABASE_URL` | Non | *Non définie (mode mémoire)* | URL de connexion PostgreSQL. Si absente, l'application fonctionne en mémoire locale. |

> [!TIP]
> Le chargement des variables d'environnement est géré de manière autonome par [`src/utils/env.ts`](file:///c:/Users/zeynb/.gemini/antigravity/scratch/Liasse-Fiscale/src/utils/env.ts) sans exiger de drapeaux CLI spécifiques.

---

## 5. Guide d'Installation & Démarrage

### Prérequis
- **Node.js** : version 20.x ou supérieure installée ([Télécharger Node.js](https://nodejs.org/))
- **npm** : gestionnaire de paquets (fourni avec Node.js)
- **PostgreSQL** : version 14 ou supérieure (optionnelle, recommandée pour la persistance)
- *(Optionnel)* **Python 3.10+** si vous utilisez des outils d'audit ou scripts auxiliaires sous environnement virtuel.

---

### Étape 1 : (Optionnel) Création et activation d'un environnement virtuel Python

Si vous travaillez avec des scripts Python ou dans un terminal configuré avec un environnement virtuel :

**Sous Windows (PowerShell) :**
```powershell
# Création de l'environnement virtuel
python -m venv venv

# Activation de l'environnement virtuel
.\venv\Scripts\Activate.ps1
```

> [!NOTE]
> Si PowerShell bloque l'exécution des scripts, exécutez au préalable :  
> `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass`

**Sous Linux / macOS :**
```bash
python3 -m venv venv
source venv/bin/activate
```

---

### Étape 2 : Installation des dépendances Node.js

À la racine du projet, installez l'ensemble des modules nécessaires :

```bash
npm install
```

---

### Étape 3 : Configuration du fichier `.env`

Créez votre fichier `.env` en dupliquant le modèle :

```bash
cp .env.example .env
```
*(ou `Copy-Item .env.example .env` sous Windows PowerShell)*

Ajustez les identifiants de votre base de données dans `DATABASE_URL` :
```env
DATABASE_URL=postgresql://postgres:votre_mot_de_passe@localhost:5432/liasse_fiscale_db
```

---

### Étape 4 : Préparation de la base de données PostgreSQL

1. **Créer la base de données dans PostgreSQL (via psql ou pgAdmin) :**
   ```sql
   CREATE DATABASE liasse_fiscale_db;
   ```

2. **Peupler la base avec les comptes et données initiales :**
   ```bash
   npm run seed-db
   ```
   Ce script exécute automatiquement [`src/db/schema.sql`](file:///c:/Users/zeynb/.gemini/antigravity/scratch/Liasse-Fiscale/src/db/schema.sql) et initialise les comptes de test et liasses d'exemple.

---

### Étape 5 : Lancement de l'Application

#### En mode développement (avec rechargement à chaud) :
```bash
npm run dev
```

#### En mode production (compilation TypeScript & exécution) :
```bash
npm run build
npm start
```

L'application est alors immédiatement disponible sur votre navigateur :  
👉 **`http://localhost:3000`**

---

## 6. Points d'Entrée de l'API

Le serveur expose une API REST unifiée documentée ci-dessous :

| Méthode | Route API | Description |
| :--- | :--- | :--- |
| `POST` | `/api/auth/login` | Authentification JWT (Matricule fiscal / Identifiant et mot de passe) |
| `GET` | `/api/auth/accounts` | Liste des comptes de test enregistrés en base |
| `GET` | `/api/contribuables/:matricule` | Consultation de la fiche d'un contribuable |
| `GET` | `/api/liasses/etats-requis` | Référentiel des états financiers selon la catégorie d'activité |
| `POST` | `/api/liasses` | Création / initialisation d'une liasse fiscale pour un exercice |
| `GET` | `/api/liasses/:id` | Détails d'une liasse et état de conformité des documents |
| `POST` | `/api/liasses/:id/documents/:code` | **Téléversement et validation d'un état financier** |
| `DELETE`| `/api/liasses/:id/documents/:code` | Détachement d'un fichier téléversé |
| `GET` | `/api/liasses/:id/documents/:code/download` | Téléchargement du fichier XML / PDF original |
| `GET` | `/api/liasses/:id/documents/:code/html` | Visualisation tabulaire et impression de l'état financier |
| `POST` | `/api/liasses/:id/verifier` | Vérification globale de la liasse avant dépôt |
| `POST` | `/api/liasses/:id/deposit` | **Dépôt officiel** de la liasse avec calcul d'empreinte SHA-256 |
| `GET` | `/api/deposits` | Historique et suivi des dépôts |
| `GET` | `/api/deposits/:reference` | Détails complets d'un dépôt |
| `GET` | `/api/deposits/:reference/receipt` | Accusé de réception officiel imprimable (SHA-256) |
| `POST` | `/api/admin/deposits/:reference/validate` | Validation administrative du dépôt (rôle DGI) |
| `POST` | `/api/admin/deposits/:reference/reject` | Rejet administratif du dépôt avec motif |

---

## 7. Comptes de Test & Fichiers d'Exemple

### Comptes Déclarants & Administrateurs Disponibles

| Identifiant / Matricule | Rôle | Mot de passe | Raison Sociale / Profil |
| :--- | :--- | :--- | :--- |
| `0000121J` | `DECLARANT` | `Password123!` | Contribuable Exemple (Système Normal) |
| `1234567M` | `DECLARANT` | `Password123!` | Société Commerciale Tunisienne |
| `1234567A` | `DECLARANT` | `Password123!` | Société Technologies Sud |
| `ADMIN` *(ou `admin@finances.gov.tn`)* | `ADMIN` | `Password123!` | Administration DGI (Contrôleur) |

---

### Fichiers d'Exemple (`/Samples`)

Des jeux d'états financiers conformes sont mis à disposition dans le répertoire `/Samples/` pour tester les téléversements et les règles arithmétiques :

- **`F6001-0000121J-2026.xml`** : Bilan - Actif (Actifs non courants, actifs courants)
- **`F6002-0000121J-2026.xml`** : Bilan - Passif (Capitaux propres, passifs non courants et courants)
- **`F6003-0000121J-2026.xml`** : État de Résultat (Produits d'exploitation, charges, résultat net)
- **`F6004-0000121J-2026.xml`** : État des Flux de Trésorerie
- **`F6019-1234567A-2024.pdf`** : Annexes & Notes aux états financiers au format PDF

---

## 8. Scripts NPM Disponibles

| Commande | Action |
| :--- | :--- |
| `npm run dev` | Démarre le serveur en mode développement avec surveillance (`tsx watch`) |
| `npm run build` | Compile l'application TypeScript vers le dossier `/dist` |
| `npm start` | Lance le serveur de production compilé |
| `npm run seed-db` | Initialise le schéma PostgreSQL et injecte les données d'exemple |
| `npm run generate-rules` | Régénère le catalogue des règles arithmétiques à partir des schémas |
| `npm run lint` | Vérifie la conformité des types TypeScript (`tsc --noEmit`) |