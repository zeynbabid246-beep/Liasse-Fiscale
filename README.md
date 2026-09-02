# Portail Officiel de Dépôt et de Suivi de la Liasse Fiscale

Portail officiel pour le téléversement, la validation réglementaire XML / XSD multi-niveaux, le dépôt sécurisé, l'horodatage certifié et le suivi de la **Liasse Fiscale** (Ministère des Finances - Direction Générale des Impôts).

---

## 1. Architecture & Technologies

L'application repose sur une architecture moderne et autonome, propulsée par un backend unique et performant en **TypeScript / Node.js** :

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

## 2. Pipeline de Validation XML Multi-Niveaux

La validation d'un état financier téléversé s'effectue selon 5 niveaux de contrôle réglementaires :

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

## 3. Principaux Points d'Entrée de l'API (`server.ts`)

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
| `POST` | `/api/liasses/:id/deposit` | **Dépôt officiel** de la liasse |
| `GET` | `/api/deposits` | Historique et suivi des dépôts |
| `GET` | `/api/deposits/:reference` | Détails complets d'un dépôt |
| `GET` | `/api/deposits/:reference/receipt` | Accusé de réception officiel imprimable (SHA-256) |
| `POST` | `/api/admin/deposits/:reference/validate` | Validation administrative du dépôt (rôle DGI) |
| `POST` | `/api/admin/deposits/:reference/reject` | Rejet administratif du dépôt avec motif |

---

## 4. Démarrage de l'Application

### Prérequis
- **Node.js** (version 20 ou supérieure)
- **npm**

### Installation et Lancement
1. **Installer les dépendances :**
   ```bash
   npm install
   ```

2. **Démarrer l'application en mode développement :**
   ```bash
   npm run dev
   ```

3. **Compiler et exécuter en mode production :**
   ```bash
   npm run build
   npm start
   ```

L'application est accessible à l'adresse : **`http://localhost:3000`**

---

## 5. Comptes de Test & Fichiers d'Exemple

### Comptes Déclarants Disponibles :
- **Contribuable Exemple** : `0000121J` (Mot de passe : `Password123!`)
- **Société Commerciale Tunisienne** : `1234567M` (Mot de passe : `Password123!`)
- **Société Technologies Sud** : `1234567A` (Mot de passe : `Password123!`)
- **Administration DGI** : `ADMIN` ou `admin@finances.gov.tn` (Mot de passe : `Password123!`)

### Fichiers d'Exemple (`/Samples`) :
Des exemples de fichiers XML et PDF conformes sont disponibles dans le dossier `/Samples/` pour tester les téléversements :
- `F6001-0000121J-2026.xml` (Bilan Actif)
- `F6002-0000121J-2026.xml` (Bilan Passif)
- `F6003-0000121J-2026.xml` (État de Résultat)
- `F6004-0000121J-2026.xml` (Flux de Trésorerie)
- `F6019-1234567A-2024.pdf` (Annexes PDF)