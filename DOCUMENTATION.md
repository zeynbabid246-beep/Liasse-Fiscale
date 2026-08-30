# Documentation Technique & Guide d'Utilisation - Liasse Fiscale (CIMF / DGI)

Cette documentation détaille l'architecture, les spécifications techniques et les procédures d'utilisation et de test du système de télé-déclaration et de dépôt sécurisé de la **Liasse Fiscale**, conforme aux directives officielles du **Ministère des Finances**, du **Centre Informatique du Ministère des Finances (CIMF)** et de la **Direction Générale des Impôts (DGI)** de la République Tunisienne.

---

## 1. Cadre Réglementaire & Objectifs

La liasse fiscale dématérialisée permet aux entreprises et professionnels assujettis de télédéclarer l'ensemble de leurs états financiers et annexes fiscales.

### Objectifs principaux :
- **Simplification administrative** : Dépôt sécurisé à distance 24h/24 et 7j/7.
- **Fiabilisation des données** : Contrôle automatique de la structure XML (XSD), cohérence des équilibres comptables et des matricules fiscaux.
- **Délivrance immédiate d'un accusé de réception** certifié électroniquement avec empreinte numérique.
- **Archivage pérenne** et réduction intégrale des supports papier.

---

## 2. Nomenclature et Règles de Nommage des Fichiers

Selon la règle définie à la **page 5 du Cahier des Charges Technique**, chaque fichier XML téléversé doit respecter la concaténation suivante :

```text
[CODE_FORMULAIRE]-[MATRICULE_FISCAL]-[EXERCICE].xml
```

- **CODE_FORMULAIRE** : Code officiel du document (ex : `F6001`, `F6002`, `F6003`, `F6004`, `F6005`, `F6007`, `F6101`, `F6201`, `F6301`, `F6401`).
- **MATRICULE_FISCAL** : Matricule fiscal sur 8 caractères (7 chiffres + 1 lettre clé, ex : `0000121J`).
- **EXERCICE** : Année fiscale sur 4 chiffres (ex : `2026`).
- **Extension** : `.xml` (ou `.pdf` pour les annexes `F6019`).

**Exemple conforme :** `F6001-0000121J-2026.xml`

---

## 3. Secteurs d'Activités & États Financiers Requis

Le système prend en charge les **6 catégories réglementaires** prévues par le cahier des charges :

### 3.1. Les autres secteurs d’activités (Cas général)
| Code Document | Libellé de l'État Financier | Format | Caractère |
|---|---|---|---|
| **F6001** | Bilan Actif | XML | **Obligatoire** |
| **F6002** | Bilan Passif | XML | **Obligatoire** |
| **F6003** | État de résultat | XML | **Obligatoire** |
| **F6004** | État de flux de trésorerie (Modèle de référence) | XML | **Obligatoire** |
| **F6005** | Tableau de détermination du résultat fiscal à partir du résultat comptable | XML | **Obligatoire** |
| **F6006** | Notes, principes comptables appliqués | XML | Optionnel |
| **F6007** | Faits marquants de l'exercice | XML | Optionnel |
| **F6019** | Autres feuillets - liasse - annexes | PDF | Optionnel |

### 3.2. Cas général (Flux de trésorerie - Modèle autorisé)
- Mêmes états que le cas général, avec l'état **F6004** structuré selon le modèle autorisé (flux indirect).

### 3.3. Secteur des banques et des établissements financiers
| Code Document | Libellé de l'État Financier | Format | Caractère |
|---|---|---|---|
| **F6101** | Bilan Actif-Passif | XML | **Obligatoire** |
| **F6103** | État de résultat | XML | **Obligatoire** |
| **F6104** | État de flux de trésorerie | XML | **Obligatoire** |
| **F6105** | État des engagements hors bilan | XML | **Obligatoire** |
| **F6005** | Tableau de détermination du résultat fiscal | XML | **Obligatoire** |
| **F6007** | Faits marquants de l'exercice | XML | Optionnel |
| **F6019** | Autres feuillets - liasse - annexes | PDF | Optionnel |

### 3.4. Secteur des assurances et des réassurances
| Code Document | Libellé de l'État Financier | Format | Caractère |
|---|---|---|---|
| **F6201** | Bilan Actif | XML | **Obligatoire** |
| **F6202** | Bilan Capitaux Propres et Passif | XML | **Obligatoire** |
| **F6205** | Résultat technique assurance non-vie | XML | **Obligatoire** |
| **F6206** | Résultat technique assurance vie | XML | **Obligatoire** |
| **F6203** | État de résultat global | XML | **Obligatoire** |
| **F6207** | Tableau des engagements reçus et donnés | XML | **Obligatoire** |
| **F6204** | État de flux de trésorerie (méthode directe) | XML | **Obligatoire** |
| **F6005** | Tableau de détermination du résultat fiscal | XML | **Obligatoire** |
| **F6007** | Faits marquants de l'exercice | XML | Optionnel |
| **F6019** | Autres feuillets - liasse - annexes | PDF | Optionnel |

### 3.5. Secteur des OPCVM (Organismes de placement collectif)
| Code Document | Libellé de l'État Financier | Format | Caractère |
|---|---|---|---|
| **F6301** | Bilan Actif-Passif | XML | **Obligatoire** |
| **F6303** | État de résultat | XML | **Obligatoire** |
| **F6304** | État de variation de l'actif net | XML | **Obligatoire** |
| **F6005** | Tableau de détermination du résultat fiscal | XML | **Obligatoire** |
| **F6006** | Notes, principes comptables appliqués | XML | Optionnel |
| **F6007** | Faits marquants de l'exercice | XML | Optionnel |
| **F6019** | Autres feuillets - liasse - annexes | PDF | Optionnel |

### 3.6. Secteur des Micro-crédits et Associations
| Code Document | Libellé de l'État Financier | Format | Caractère |
|---|---|---|---|
| **F6401** | Bilan Actif | XML | **Obligatoire** |
| **F6403** | État de résultat | XML | **Obligatoire** |
| **F6404** | État de flux de trésorerie | XML | **Obligatoire** |
| **F6005** | Tableau de détermination du résultat fiscal | XML | **Obligatoire** |
| **F6007** | Faits marquants de l'exercice | XML | Optionnel |
| **F6019** | Autres feuillets - liasse - annexes | PDF | Optionnel |

---

## 4. Spécifications du Schéma XML et de l'Entête `T_Entete`

Tous les fichiers XML doivent respecter la syntaxe XML 1.0 (UTF-8) avec le namespace officiel :

```xml
<?xml version="1.0" encoding="UTF-8"?>
<lf:F6001 xmlns:lf="http://www.impots.finances.gov.tn/liasse" 
          xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" 
          xsi:schemaLocation="http://www.impots.finances.gov.tn/liasse F6001.xsd">
  <lf:Entete>
    <lf:MatriculeFiscalDeclarant>0000121J</lf:MatriculeFiscalDeclarant>
    <lf:NometPrenomouRaisonSociale>SOCIETE EXEMPLE SARL</lf:NometPrenomouRaisonSociale>
    <lf:Activite>COMMERCE ET SERVICES</lf:Activite>
    <lf:Adresse>AVENUE HABIB BOURGUIBA, TUNIS</lf:Adresse>
    <lf:Exercice>2026</lf:Exercice>
    <lf:DateDebutExercice>01/01/2026</lf:DateDebutExercice>
    <lf:DateClotureExercice>31/12/2026</lf:DateClotureExercice>
    <lf:ActeDeDepot>0</lf:ActeDeDepot>
    <lf:NatureDepot>D</lf:NatureDepot>
  </lf:Entete>
  
  <!-- Données déclaratives détaillées (selon le formulaire) -->
  <lf:F60010001>1850000</lf:F60010001>
  <lf:F60010002>1850000</lf:F60010002>
  <lf:F60010068>3000000</lf:F60010068>
  <lf:F60011068>650000</lf:F60011068>
  <lf:F60012068>2350000</lf:F60012068>
  <lf:F60013068>2100000</lf:F60013068>
</lf:F6001>
```

### Valeurs autorisées pour l'entête :
- **ActeDeDepot** :
  - `0` : Spontané
  - `1` : Rectification
  - `2` : Régularisation
- **NatureDepot** :
  - `D` : Dépôt définitif
  - `P` : Dépôt provisoire

---

## 5. Guide Pas à Pas pour Tester le Projet

Vous pouvez tester l'ensemble du cycle de vie directement depuis l'interface web :

### Étape 1 : Connexion
- **Identifiant** : `declarant@finances.gov.tn` (ou tout identifiant adhérent)
- **Mot de passe** : `Password123!`
- Cliquez sur **Se connecter**.

### Étape 2 : Identification du Contribuable
- Matricule fiscal : `0000121`
- Clé : `J`
- Cliquez sur **🔍 Rechercher**. Les coordonnées de la société s'affichent automatiquement.

### Étape 3 : Dépôt de la Liasse (Onglet "Dépôt")
1. Choisissez une catégorie (ex: **Banques** ou **Cas général**).
2. Sélectionnez l'exercice (ex: **2026**).
3. Vous pouvez sélectionner vos propres fichiers XML via **Parcourir…** ou cliquer directement sur **Uploader Liasse** pour générer et attacher l'ensemble des fichiers conformes au cahier des charges.
4. Cliquez sur **Vérifier Liasse** : le système valide la présence et la conformité de chaque état obligatoire.

### Étape 4 : Validation (Onglet "Valider le dépôt en cours")
1. Consultez la liasse en cours de saisie.
2. Vous avez la possibilité de :
   - **Valider le dépôt** : finalise la télé-déclaration et génère la référence officielle.
   - **✕ Supprimer** : annule la liasse en cours (qui sera consignée avec le statut `Supprimée` dans l'historique conformément à la page 6 et 7 du guide).

### Étape 5 : Suivi et Accusé de Réception (Onglet "Suivi des dépôts")
1. Visualisez l'historique complet des dépôts (`Validée`, `Supprimée`, `En cours`).
2. Cliquez sur **📄 Accusé de réception** pour afficher et imprimer l'attestation officielle certifiée avec empreinte SHA256.
3. Pour chaque document déposé :
   - **▼ Télécharger** : télécharge le fichier XML ou PDF d'origine.
   - **📄 Consulter** : affiche la restitution tabulaire financière HTML complète avec calcul des masses comptables.

### Étape 6 : Exécution du Banc d'Essais Automatique
- Cliquez sur le bouton **🧪 Tests Automatiques** situé dans l'entête supérieure pour exécuter en direct les 8 scénarios de test de conformité.
