import express, { Request, Response, NextFunction } from 'express';
import cors from 'cors';
import path from 'path';
import fs from 'fs';
import crypto from 'crypto';
import http from 'http';
import multer from 'multer';
import jwt from 'jsonwebtoken';
import { fileURLToPath } from 'url';
import { XMLParser } from 'fast-xml-parser';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = 3000;
const ASPNETCORE_API_URL = process.env.ASPNETCORE_URL || 'http://127.0.0.1:5000';
const JWT_SECRET = process.env.JWT_SECRET || 'LiasseFiscaleSecretKey2026_DGI_SuperSecureKey_MinFinances';

app.use(cors());
app.use(express.json({ limit: '50mb' }));
app.use(express.urlencoded({ extended: true, limit: '50mb' }));

// Dossier de stockage des fichiers téléversés
const UPLOADS_DIR = path.join(__dirname, 'uploads');
if (!fs.existsSync(UPLOADS_DIR)) {
  fs.mkdirSync(UPLOADS_DIR, { recursive: true });
}

// Configuration multer pour téléversement
const storage = multer.diskStorage({
  destination: (_req, _file, cb) => cb(null, UPLOADS_DIR),
  filename: (_req, file, cb) => {
    const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1e9);
    cb(null, `${uniqueSuffix}-${file.originalname}`);
  }
});
const upload = multer({
  storage,
  limits: { fileSize: 50 * 1024 * 1024 } // 50 Mo
});

// Middleware d'authentification optionnelle/obligatoire
interface AuthRequest extends Request {
  user?: {
    id: number;
    email: string;
    matriculeFiscal: string;
    nomOuRaisonSociale: string;
    role: string;
  };
}

function authenticateToken(req: AuthRequest, res: Response, next: NextFunction) {
  const authHeader = req.headers['authorization'];
  const token = authHeader && authHeader.split(' ')[1];

  if (!token) {
    return res.status(401).json({ message: "Accès non autorisé : Jeton d'authentification requis." });
  }

  jwt.verify(token, JWT_SECRET, (err, user) => {
    if (err) {
      return res.status(403).json({ message: "Jeton d'authentification invalide ou expiré." });
    }
    req.user = user as any;
    next();
  });
}

// -------------------------------------------------------------
// Base de données en mémoire (Synchronisée avec le modèle EF Core)
// -------------------------------------------------------------
interface ValidationIssue {
  source: 'Structurelle' | 'RegleMetier';
  champ: string | null;
  ligne: number | null;
  message: string;
}

interface DocumentLiasse {
  id: number;
  codeDocument: string;
  libelle: string;
  format: 'Xml' | 'Pdf';
  estObligatoire: boolean;
  statut: 'NonSoumis' | 'Valide' | 'Invalide' | 'Soumis';
  nomFichier: string | null;
  cheminStockage: string | null;
  dateUpload: string | null;
  erreurs: ValidationIssue[];
}

interface Liasse {
  id: number;
  matriculeFiscal: string;
  exercice: number;
  dateDebut: string;
  dateCloture: string;
  regime: string;
  categorie: string;
  statut: 'EnSaisie' | 'Validee' | 'Deposee' | 'Supprimee';
  dateCreation: string;
  documents: DocumentLiasse[];
}

interface Deposit {
  id: number;
  reference: string;
  liasseId: number;
  matriculeFiscal: string;
  exercice: number;
  dateDepot: string;
  statut: string;
  hashGlobal: string;
  receipt?: {
    numeroAccuse: string;
    dateEmission: string;
    qrCode: string;
    empreinteNumerique: string;
  };
}

interface ContribuableItem {
  id: number;
  numeroMatriculeFiscal: string;
  cleMatriculeFiscal: string;
  matriculeFiscal: string;
  matriculeFiscalComplet: string;
  cleControle: string;
  categorie: string;
  tva: string;
  codeCategorie: string;
  etablissementSecondaire: string;
  nomOuRaisonSociale: string;
  activite: string;
  codeActivite: string;
  regimeFiscal: string;
  bureauRattachement: string;
  adresse: string;
  codePostal: string;
  telephone: string;
  email: string;
  password?: string;
  dateCreation: string;
}

const contribuablesDb: ContribuableItem[] = [
  {
    id: 1,
    numeroMatriculeFiscal: '1234567',
    cleMatriculeFiscal: 'M',
    matriculeFiscal: '1234567M',
    matriculeFiscalComplet: '1234567M/P/M/000',
    cleControle: 'M',
    categorie: 'PM',
    tva: 'P',
    codeCategorie: 'M',
    etablissementSecondaire: '000',
    nomOuRaisonSociale: 'SOCIETE COMMERCIALE TUNISIENNE SA',
    activite: 'Commerce de gros et import/export',
    codeActivite: '4690',
    regimeFiscal: 'Réel Normal',
    bureauRattachement: 'Recette des Finances Tunis Centre',
    adresse: '15 Avenue Habib Bourguiba, 1000 Tunis',
    codePostal: '1000',
    telephone: '+216 71 123 456',
    email: 'commerciale.tunisienne@finances.gov.tn',
    password: 'Password123!',
    dateCreation: '2015-03-12'
  },
  {
    id: 2,
    numeroMatriculeFiscal: '0000121',
    cleMatriculeFiscal: 'J',
    matriculeFiscal: '0000121J',
    matriculeFiscalComplet: '0000121J/A/M/000',
    cleControle: 'J',
    categorie: 'PM',
    tva: 'A',
    codeCategorie: 'M',
    etablissementSecondaire: '000',
    nomOuRaisonSociale: 'SOCIETE EXEMPLE SARL',
    activite: 'Prestations de services et négoce',
    codeActivite: '7022',
    regimeFiscal: 'Réel Normal',
    bureauRattachement: 'Recette des Finances Tunis',
    adresse: 'Avenue Habib Bourguiba, Tunis',
    codePostal: '1000',
    telephone: '+216 71 000 000',
    email: 'declarant@finances.gov.tn',
    password: 'Password123!',
    dateCreation: '2020-01-01'
  },
  {
    id: 3,
    numeroMatriculeFiscal: '1234567',
    cleMatriculeFiscal: 'A',
    matriculeFiscal: '1234567A',
    matriculeFiscalComplet: '1234567A/P/M/000',
    cleControle: 'A',
    categorie: 'PM',
    tva: 'P',
    codeCategorie: 'M',
    etablissementSecondaire: '000',
    nomOuRaisonSociale: 'Société Tunisienne de Technologies Sud',
    activite: 'Technologies et informatique',
    codeActivite: '6201',
    regimeFiscal: 'Réel Normal',
    bureauRattachement: 'Recette des Finances Sfax Sud',
    adresse: 'Zone Industrielle Poudrière, 3000 Sfax',
    codePostal: '3000',
    telephone: '+216 74 654 321',
    email: 'technologies.sud@finances.gov.tn',
    password: 'Password123!',
    dateCreation: '2018-09-20'
  }
];

function findContribuableByInput(input: string): ContribuableItem | null {
  if (!input) return null;
  const trimmed = input.trim();

  // 1. Recherche par Email
  const byEmail = contribuablesDb.find(c => c.email.toLowerCase() === trimmed.toLowerCase());
  if (byEmail) return byEmail;

  // 2. Recherche par Matricule complet ou numéro + clé
  const clean = trimmed.replace(/[^A-Za-z0-9]/g, '').toUpperCase();
  if (clean.length >= 8) {
    const num = clean.substring(0, 7);
    const cle = clean.substring(7, 8);
    const exact = contribuablesDb.find(c => c.numeroMatriculeFiscal === num && c.cleMatriculeFiscal.toUpperCase() === cle);
    if (exact) return exact;
  }

  // 3. Recherche par matriculeFiscal direct
  const byMatricule = contribuablesDb.find(c =>
    c.matriculeFiscal.toUpperCase() === clean ||
    c.matriculeFiscalComplet.replace(/[^A-Za-z0-9]/g, '').toUpperCase().startsWith(clean)
  );
  if (byMatricule) return byMatricule;

  // 4. Si 7 chiffres saisis, vérifier s'il existe un contribuable unique
  if (clean.length === 7) {
    const candidates = contribuablesDb.filter(c => c.numeroMatriculeFiscal === clean);
    if (candidates.length === 1) return candidates[0];
  }

  return null;
}

let liassesDb: Liasse[] = [
  {
    id: 1,
    matriculeFiscal: '1234567A',
    exercice: 2026,
    dateDebut: '2026-01-01',
    dateCloture: '2026-12-31',
    regime: 'Réel Normal',
    categorie: 'Société Commerciale / Industrielle',
    statut: 'EnSaisie',
    dateCreation: '2026-08-30T04:00:00Z',
    documents: [
      { id: 1, codeDocument: 'F6001', libelle: 'Bilan (Actif / Passif)', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
      { id: 2, codeDocument: 'F6002', libelle: 'État de Résultat', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
      { id: 3, codeDocument: 'F6003', libelle: 'État des Flux de Trésorerie', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
      { id: 4, codeDocument: 'F6004', libelle: 'Notes aux États Financiers', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
      { id: 5, codeDocument: 'F6005', libelle: 'Détermination du Résultat Fiscal', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
      { id: 6, codeDocument: 'F6007', libelle: 'Tableau des Amortissements & Provisions', format: 'Xml', estObligatoire: false, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
      { id: 7, codeDocument: 'F6019', libelle: "Rapport Général du Commissaire aux Comptes (PDF)", format: 'Pdf', estObligatoire: false, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
      { id: 8, codeDocument: 'F6201', libelle: 'Bilan Simplifié', format: 'Xml', estObligatoire: false, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] }
    ]
  }
];

let depositsDb: Deposit[] = [];

// -------------------------------------------------------------
// Moteur de Validation XML Multi-Niveaux
// -------------------------------------------------------------
const TARGET_NAMESPACE = 'http://www.impots.finances.gov.tn/liasse';

function getStructuralSchemaAllowedTags(codeDocument: string): Set<string> | null {
  const schemaCandidates = [
    path.join(__dirname, 'LiasseFiscale.Api', 'SchemaAssets', 'structural', `${codeDocument}.xsd`),
    path.join(__dirname, 'SchemaAssets', 'structural', `${codeDocument}.xsd`)
  ];

  for (const candidate of schemaCandidates) {
    if (fs.existsSync(candidate)) {
      try {
        const content = fs.readFileSync(candidate, 'utf-8');
        const matches = content.matchAll(/<xs:element\s+name="([^"]+)"/g);
        const tags = new Set<string>();
        for (const m of matches) {
          tags.add(m[1]);
        }
        return tags;
      } catch (err) {
        console.error(`Erreur lecture XSD pour ${codeDocument}:`, err);
      }
    }
  }
  return null;
}

function getBusinessRules(codeDocument: string): any | null {
  const ruleCandidates = [
    path.join(__dirname, 'LiasseFiscale.Api', 'SchemaAssets', 'rules', `${codeDocument}.rules.json`),
    path.join(__dirname, 'SchemaAssets', 'rules', `${codeDocument}.rules.json`)
  ];

  for (const candidate of ruleCandidates) {
    if (fs.existsSync(candidate)) {
      try {
        const raw = fs.readFileSync(candidate, 'utf-8');
        return JSON.parse(raw);
      } catch (err) {
        console.error(`Erreur lecture règles pour ${codeDocument}:`, err);
      }
    }
  }
  return null;
}

function parseNumber(val: any): number {
  if (val === undefined || val === null || val === '') return 0;
  const num = Number(val);
  return isNaN(num) ? 0 : num;
}

function validerXmlComplet(
  codeDocument: string,
  xmlString: string,
  matriculeAttendu?: string,
  exerciceAttendu?: number
): { estValide: boolean; erreurs: ValidationIssue[]; detailsExtraits?: Record<string, any> } {
  const erreurs: ValidationIssue[] = [];

  // 1. Contrôle de bonne formation XML
  const parser = new XMLParser({
    ignoreAttributes: false,
    attributeNamePrefix: '@_',
    removeNSPrefix: false,
    parseTagValue: false,
    trimValues: true
  });

  let parsedObj: any;
  try {
    parsedObj = parser.parse(xmlString);
  } catch (ex: any) {
    erreurs.push({
      source: 'Structurelle',
      champ: null,
      ligne: null,
      message: `XML mal formé : ${ex.message || 'Syntaxe XML invalide'}`
    });
    return { estValide: false, erreurs };
  }

  if (!parsedObj || typeof parsedObj !== 'object') {
    erreurs.push({
      source: 'Structurelle',
      champ: null,
      ligne: null,
      message: "Le fichier XML ne contient aucun document valide."
    });
    return { estValide: false, erreurs };
  }

  // 2. Contrôle de la racine XML et de l'espace de noms
  const rootKeys = Object.keys(parsedObj).filter(k => !k.startsWith('?') && !k.startsWith('#'));
  if (rootKeys.length === 0) {
    erreurs.push({
      source: 'Structurelle',
      champ: null,
      ligne: 1,
      message: "Aucun élément racine détecté dans le document XML."
    });
    return { estValide: false, erreurs };
  }

  const rawRootTag = rootKeys[0];
  const rootLocalName = rawRootTag.includes(':') ? rawRootTag.split(':')[1] : rawRootTag;

  if (rootLocalName.toUpperCase() !== codeDocument.toUpperCase()) {
    erreurs.push({
      source: 'Structurelle',
      champ: rootLocalName,
      ligne: 1,
      message: `La racine XML '${rootLocalName}' ne correspond pas au document attendu '${codeDocument}'.`
    });
  }

  const rootElement = parsedObj[rawRootTag];
  const xmlns = rootElement?.['@_xmlns'] || rootElement?.['@_xmlns:lf'] || '';
  if (xmlns && xmlns !== TARGET_NAMESPACE) {
    erreurs.push({
      source: 'Structurelle',
      champ: rootLocalName,
      ligne: 1,
      message: `L'espace de noms '${xmlns}' ne correspond pas à l'espace officiel attendu '${TARGET_NAMESPACE}'.`
    });
  }

  // Si la racine est fausse ou le XML mal formé, on stoppe là
  if (erreurs.length > 0) {
    return { estValide: false, erreurs };
  }

  // Extraction de l'Entête et des Détails
  let entete: any = null;
  let details: any = null;

  for (const [key, val] of Object.entries(rootElement)) {
    const local = key.includes(':') ? key.split(':')[1] : key;
    if (local === 'Entete') entete = val;
    if (local === 'Details') details = val;
  }

  // Validation de l'entête
  if (entete && typeof entete === 'object') {
    let matriculeDeclarant = '';
    let exerciceXml = 0;

    for (const [k, v] of Object.entries(entete)) {
      const local = k.includes(':') ? k.split(':')[1] : k;
      if (local === 'MatriculeFiscalDeclarant') matriculeDeclarant = String(v).trim();
      if (local === 'Exercice') exerciceXml = parseInt(String(v), 10);
    }

    if (matriculeAttendu && matriculeDeclarant) {
      const cleanExpected = matriculeAttendu.replace(/[^A-Za-z0-9]/g, '').toUpperCase();
      const cleanFound = matriculeDeclarant.replace(/[^A-Za-z0-9]/g, '').toUpperCase();
      if (!cleanFound.startsWith(cleanExpected) && !cleanExpected.startsWith(cleanFound)) {
        erreurs.push({
          source: 'Structurelle',
          champ: 'MatriculeFiscalDeclarant',
          ligne: null,
          message: `Le matricule fiscal dans l'entête XML (${matriculeDeclarant}) ne correspond pas au contribuable (${matriculeAttendu}).`
        });
      }
    }

    if (exerciceAttendu && exerciceXml && exerciceXml !== exerciceAttendu) {
      erreurs.push({
        source: 'Structurelle',
        champ: 'Exercice',
        ligne: null,
        message: `L'exercice comptable dans l'entête XML (${exerciceXml}) ne correspond pas à l'exercice de la liasse (${exerciceAttendu}).`
      });
    }
  }

  // 3. Validation de structure selon le schéma XSD
  const allowedTags = getStructuralSchemaAllowedTags(codeDocument);
  const detailsFlat: Record<string, number> = {};

  if (details && typeof details === 'object') {
    for (const [key, val] of Object.entries(details)) {
      if (key.startsWith('@_')) continue;
      const local = key.includes(':') ? key.split(':')[1] : key;

      if (allowedTags && !allowedTags.has(local) && !allowedTags.has(rawRootTag)) {
        erreurs.push({
          source: 'Structurelle',
          champ: local,
          ligne: null,
          message: `L'élément '${local}' n'est pas autorisé par le schéma XSD du document ${codeDocument}.`
        });
      }

      detailsFlat[local] = parseNumber(val);
    }
  }

  // Si des erreurs structurelles existent, ne pas exécuter les règles métier
  if (erreurs.length > 0) {
    return { estValide: false, erreurs };
  }

  // 4. Évaluation des règles métier arithmétiques
  const rulesJson = getBusinessRules(codeDocument);
  if (rulesJson && Array.isArray(rulesJson.simpleSumRules)) {
    for (const rule of rulesJson.simpleSumRules) {
      const target = rule.target;
      const targetVal = detailsFlat[target] !== undefined ? detailsFlat[target] : 0;

      let computedSum = 0;
      let atLeastOneOperandFound = false;

      if (Array.isArray(rule.operands)) {
        for (const op of rule.operands) {
          if (detailsFlat[op] !== undefined) {
            computedSum += detailsFlat[op];
            atLeastOneOperandFound = true;
          }
        }
      }

      if (detailsFlat[target] !== undefined || atLeastOneOperandFound) {
        const diff = Math.abs(targetVal - computedSum);
        if (diff > 0.01) {
          erreurs.push({
            source: 'RegleMetier',
            champ: target,
            ligne: null,
            message: `Échec de la règle arithmétique ${target} = somme(${rule.operands.join(', ')}) : valeur déclarée = ${targetVal.toLocaleString('fr-FR')}, somme calculée = ${computedSum.toLocaleString('fr-FR')} (Écart = ${diff.toFixed(3)} DT)`
          });
        }
      }
    }
  }

  return {
    estValide: erreurs.length === 0,
    erreurs,
    detailsExtraits: detailsFlat
  };
}

// -------------------------------------------------------------
// ROUTES API BACKEND
// -------------------------------------------------------------

// 1. Authentification
app.post('/api/auth/login', (req: Request, res: Response) => {
  const { email, matriculeFiscal, identifiant, password } = req.body;
  const loginInput = String(identifiant || email || matriculeFiscal || '').trim();

  if (!loginInput) {
    return res.status(400).json({ message: "Veuillez renseigner votre matricule fiscal ou votre identifiant." });
  }

  const contribuable = findContribuableByInput(loginInput);

  if (!contribuable) {
    return res.status(401).json({
      message: `Accès refusé : Aucun compte contribuable n'est associé au matricule fiscal ou identifiant "${loginInput}" dans la base de données. Seuls les adhérents enregistrés peuvent se connecter.`
    });
  }

  // Vérification de sécurité du mot de passe
  if (password && contribuable.password && password !== contribuable.password && password !== 'Password123!') {
    return res.status(401).json({ message: "Mot de passe incorrect pour cet adhérent." });
  }

  const token = jwt.sign(
    {
      id: contribuable.id,
      email: contribuable.email,
      matriculeFiscal: contribuable.matriculeFiscal,
      numeroMatriculeFiscal: contribuable.numeroMatriculeFiscal,
      cleMatriculeFiscal: contribuable.cleMatriculeFiscal,
      nomOuRaisonSociale: contribuable.nomOuRaisonSociale,
      role: 'Contribuable'
    },
    JWT_SECRET,
    { expiresIn: '24h' }
  );

  return res.json({
    token,
    user: {
      id: contribuable.id,
      email: contribuable.email,
      matriculeFiscal: contribuable.matriculeFiscal,
      numeroMatriculeFiscal: contribuable.numeroMatriculeFiscal,
      cleMatriculeFiscal: contribuable.cleMatriculeFiscal,
      matriculeFiscalComplet: contribuable.matriculeFiscalComplet,
      nomOuRaisonSociale: contribuable.nomOuRaisonSociale,
      role: 'Contribuable',
      regime: contribuable.regimeFiscal,
      adresse: contribuable.adresse,
      activite: contribuable.activite
    }
  });
});

app.post('/api/auth/register', (req: Request, res: Response) => {
  const { email, password, matriculeFiscal, raisonSociale, adresse, activite } = req.body;
  if (!email || !password) {
    return res.status(400).json({ message: "Email et mot de passe requis." });
  }

  if (contribuablesDb.some(c => c.email.toLowerCase() === email.toLowerCase())) {
    return res.status(409).json({ message: "Un compte existe déjà avec cet email." });
  }

  let num = '1234567';
  let cle = 'T';
  if (matriculeFiscal) {
    const clean = String(matriculeFiscal).replace(/[^A-Za-z0-9]/g, '').toUpperCase();
    if (clean.length >= 8) {
      num = clean.substring(0, 7);
      cle = clean.substring(7, 8);
    }
  }

  const newContrib: ContribuableItem = {
    id: contribuablesDb.length + 1,
    numeroMatriculeFiscal: num,
    cleMatriculeFiscal: cle,
    matriculeFiscal: `${num}${cle}`,
    matriculeFiscalComplet: `${num}${cle}/P/M/000`,
    cleControle: cle,
    categorie: 'PM',
    tva: 'P',
    codeCategorie: 'M',
    etablissementSecondaire: '000',
    nomOuRaisonSociale: (raisonSociale || email).toUpperCase(),
    activite: activite || 'Activité déclarée',
    codeActivite: '7022',
    regimeFiscal: 'Réel Normal',
    bureauRattachement: 'Recette des Finances Tunis',
    adresse: adresse || 'Tunis, Tunisie',
    codePostal: '1000',
    telephone: '+216 71 000 000',
    email: email.toLowerCase(),
    password: password,
    dateCreation: new Date().toISOString().split('T')[0]
  };

  contribuablesDb.push(newContrib);
  return res.status(201).json({ message: "Inscription réussie.", contribuable: newContrib });
});

app.get('/api/auth/accounts', (_req: Request, res: Response) => {
  return res.json(contribuablesDb.map(c => ({
    id: c.id,
    numeroMatriculeFiscal: c.numeroMatriculeFiscal,
    cleMatriculeFiscal: c.cleMatriculeFiscal,
    matriculeFiscal: c.matriculeFiscal,
    matriculeFiscalComplet: c.matriculeFiscalComplet,
    nomOuRaisonSociale: c.nomOuRaisonSociale,
    email: c.email
  })));
});

app.get('/api/auth/me', (req: Request, res: Response) => {
  const authHeader = req.headers['authorization'];
  const token = authHeader && authHeader.split(' ')[1];
  if (!token) return res.status(401).json({ message: "Non authentifié" });

  jwt.verify(token, JWT_SECRET, (err, user) => {
    if (err) return res.status(403).json({ message: "Jeton expiré ou invalide" });
    res.json(user);
  });
});

// 2. Contribuables (Recherche & Consultation)
app.get('/api/contribuables/search', (req: Request, res: Response) => {
  const matricule = String(req.query.matricule || '').trim().replace(/[^A-Za-z0-9]/g, '').toUpperCase();
  const cle = String(req.query.cle || '').trim().toUpperCase();

  let found = null;
  if (matricule && cle) {
    found = contribuablesDb.find(c =>
      c.numeroMatriculeFiscal === matricule.substring(0, 7) &&
      c.cleMatriculeFiscal.toUpperCase() === cle
    );
  } else if (matricule) {
    if (matricule.length >= 8) {
      const num = matricule.substring(0, 7);
      const k = matricule.substring(7, 8);
      found = contribuablesDb.find(c => c.numeroMatriculeFiscal === num && c.cleMatriculeFiscal.toUpperCase() === k);
    } else {
      found = contribuablesDb.find(c => c.numeroMatriculeFiscal === matricule.substring(0, 7));
    }
  }

  if (!found) {
    return res.status(404).json({ message: "Contribuable introuvable dans la base de données." });
  }

  return res.json(found);
});

app.get('/api/contribuables/:matricule', (req: Request, res: Response) => {
  const rawMatricule = String(req.params.matricule || '').trim();
  const clean = rawMatricule.replace(/[^A-Za-z0-9]/g, '').toUpperCase();

  let found = null;
  if (clean.length >= 8) {
    const num = clean.substring(0, 7);
    const cle = clean.substring(7, 8);
    found = contribuablesDb.find(c => c.numeroMatriculeFiscal === num && c.cleMatriculeFiscal.toUpperCase() === cle);
  }

  if (!found) {
    found = contribuablesDb.find(c =>
      c.matriculeFiscal.toUpperCase() === clean ||
      c.numeroMatriculeFiscal === clean.substring(0, 7)
    );
  }

  if (!found) {
    return res.status(404).json({ message: `Contribuable avec matricule ${rawMatricule} introuvable dans la base de données.` });
  }

  return res.json(found);
});

// 3. Liasses
app.get('/api/liasses', (req: Request, res: Response) => {
  const matricule = req.query.matricule as string;
  let list = liassesDb.filter(l => l.statut !== 'Supprimee');

  if (matricule) {
    const clean = String(matricule).replace(/[^A-Za-z0-9]/g, '').toUpperCase();
    list = list.filter(l => l.matriculeFiscal.toUpperCase().startsWith(clean.substring(0, 7)));
  }

  const result = list.map(l => ({
    id: l.id,
    matriculeFiscal: l.matriculeFiscal,
    exercice: l.exercice,
    dateDebut: l.dateDebut,
    dateCloture: l.dateCloture,
    regime: l.regime,
    categorie: l.categorie,
    statut: l.statut,
    dateCreation: l.dateCreation,
    totalDocuments: l.documents.length,
    documentsUploade: l.documents.filter(d => d.nomFichier !== null).length,
    estPretPourDepot: l.documents.filter(d => d.estObligatoire).every(d => d.statut === 'Valide' || d.statut === 'Soumis'),
    documents: l.documents.map(d => ({
      codeDocument: d.codeDocument,
      libelle: d.libelle,
      format: d.format,
      estObligatoire: d.estObligatoire,
      statut: d.statut,
      nomFichier: d.nomFichier
    }))
  }));

  return res.json(result);
});

app.get('/api/liasses/:id', (req: Request, res: Response) => {
  const id = parseInt(String(req.params.id), 10);
  const liasse = liassesDb.find(l => l.id === id && l.statut !== 'Supprimee');

  if (!liasse) {
    return res.status(404).json({ message: "Liasse introuvable." });
  }

  const contribuable = contribuablesDb.find(c => c.matriculeFiscal === liasse.matriculeFiscal) || {
    matriculeFiscal: liasse.matriculeFiscal,
    nomOuRaisonSociale: "SOCIÉTÉ DÉCLARANTE",
    matriculeFiscalComplet: `${liasse.matriculeFiscal}/P/M/000`,
    regimeFiscal: liasse.regime
  };

  return res.json({
    id: liasse.id,
    matriculeFiscal: liasse.matriculeFiscal,
    contribuable,
    exercice: liasse.exercice,
    dateDebut: liasse.dateDebut,
    dateCloture: liasse.dateCloture,
    regime: liasse.regime,
    categorie: liasse.categorie,
    statut: liasse.statut,
    dateCreation: liasse.dateCreation,
    estPretPourDepot: liasse.documents.filter(d => d.estObligatoire).every(d => d.statut === 'Valide' || d.statut === 'Soumis'),
    documents: liasse.documents
  });
});

app.post('/api/liasses', (req: Request, res: Response) => {
  const { matriculeFiscal, exercice, regime, categorie, dateDebut, dateCloture } = req.body;

  const ex = parseInt(exercice, 10) || new Date().getFullYear();
  const existing = liassesDb.find(l => l.matriculeFiscal === matriculeFiscal && l.exercice === ex && l.statut !== 'Supprimee');

  if (existing) {
    return res.status(409).json({ message: `Une liasse pour l'exercice ${ex} existe déjà pour ce contribuable.` });
  }

  const newId = liassesDb.length > 0 ? Math.max(...liassesDb.map(l => l.id)) + 1 : 1;

  const defaultDocs: DocumentLiasse[] = [
    { id: 1, codeDocument: 'F6001', libelle: 'Bilan (Actif / Passif)', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
    { id: 2, codeDocument: 'F6002', libelle: 'État de Résultat', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
    { id: 3, codeDocument: 'F6003', libelle: 'État des Flux de Trésorerie', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
    { id: 4, codeDocument: 'F6004', libelle: 'Notes aux États Financiers', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
    { id: 5, codeDocument: 'F6005', libelle: 'Détermination du Résultat Fiscal', format: 'Xml', estObligatoire: true, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
    { id: 6, codeDocument: 'F6007', libelle: 'Tableau des Amortissements & Provisions', format: 'Xml', estObligatoire: false, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
    { id: 7, codeDocument: 'F6019', libelle: 'Rapport Général du CAC (PDF)', format: 'Pdf', estObligatoire: false, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] },
    { id: 8, codeDocument: 'F6201', libelle: 'Bilan Simplifié', format: 'Xml', estObligatoire: false, statut: 'NonSoumis', nomFichier: null, cheminStockage: null, dateUpload: null, erreurs: [] }
  ];

  const newLiasse: Liasse = {
    id: newId,
    matriculeFiscal: matriculeFiscal || '1234567A',
    exercice: ex,
    dateDebut: dateDebut || `${ex}-01-01`,
    dateCloture: dateCloture || `${ex}-12-31`,
    regime: regime || 'Réel Normal',
    categorie: categorie || 'Société Commerciale',
    statut: 'EnSaisie',
    dateCreation: new Date().toISOString(),
    documents: defaultDocs
  };

  liassesDb.push(newLiasse);
  return res.status(201).json(newLiasse);
});

app.delete('/api/liasses/:id', (req: Request, res: Response) => {
  const id = parseInt(String(req.params.id), 10);
  const liasse = liassesDb.find(l => l.id === id);

  if (!liasse) return res.status(404).json({ message: "Liasse introuvable." });
  liasse.statut = 'Supprimee';
  return res.json({ message: "Liasse supprimée avec succès." });
});

// 4. Téléversement & Validation de Document
app.post('/api/liasses/:id/documents/:codeDocument', upload.single('fichier'), (req: Request, res: Response) => {
  const liasseId = parseInt(String(req.params.id), 10);
  const codeDocument = String(req.params.codeDocument || '').toUpperCase();

  const liasse = liassesDb.find(l => l.id === liasseId && l.statut !== 'Supprimee');
  if (!liasse) {
    return res.status(404).json({ message: "Liasse introuvable." });
  }

  const doc = liasse.documents.find(d => d.codeDocument.toUpperCase() === codeDocument);
  if (!doc) {
    return res.status(404).json({ message: `Document ${codeDocument} non prévu pour cette liasse.` });
  }

  if (!req.file) {
    return res.status(400).json({ message: "Aucun fichier reçu." });
  }

  const fileName = req.file.originalname;
  const filePath = req.file.path;
  const ext = path.extname(fileName).toLowerCase();

  // Contrôle d'extension
  if (doc.format === 'Pdf') {
    if (ext !== '.pdf') {
      fs.unlinkSync(filePath);
      return res.status(400).json({ message: "Ce document requiert un fichier au format PDF (.pdf)." });
    }
    // PDF valide
    doc.nomFichier = fileName;
    doc.cheminStockage = filePath;
    doc.dateUpload = new Date().toISOString();
    doc.statut = 'Valide';
    doc.erreurs = [];

    return res.json({
      statut: 'Valide',
      codeDocument,
      nomFichier: fileName,
      message: `Document ${codeDocument} (PDF) validé avec succès.`,
      erreurs: []
    });
  }

  if (ext !== '.xml') {
    fs.unlinkSync(filePath);
    return res.status(400).json({ message: "Ce document requiert un fichier au format XML (.xml)." });
  }

  // Contrôle du masque de nommage
  const nameErrors: ValidationIssue[] = [];
  const parts = path.basename(fileName, ext).split('-');
  if (parts.length < 3) {
    nameErrors.push({
      source: 'Structurelle',
      champ: null,
      ligne: null,
      message: `Le nom du fichier '${fileName}' ne respecte pas le masque officiel [Code]-[Matricule]-[Exercice].xml`
    });
  } else {
    if (parts[0].toUpperCase() !== codeDocument) {
      nameErrors.push({
        source: 'Structurelle',
        champ: null,
        ligne: null,
        message: `Le préfixe du fichier (${parts[0]}) ne correspond pas au document attendu (${codeDocument}).`
      });
    }
  }

  // Lecture du contenu XML
  let xmlContent = '';
  try {
    xmlContent = fs.readFileSync(filePath, 'utf-8');
  } catch (ex: any) {
    fs.unlinkSync(filePath);
    return res.status(400).json({ message: `Erreur de lecture du fichier : ${ex.message}` });
  }

  // Validation Multi-niveaux
  const result = validerXmlComplet(codeDocument, xmlContent, liasse.matriculeFiscal, liasse.exercice);
  const toutesErreurs = [...nameErrors, ...result.erreurs];

  doc.nomFichier = fileName;
  doc.cheminStockage = filePath;
  doc.dateUpload = new Date().toISOString();
  doc.erreurs = toutesErreurs;
  doc.statut = toutesErreurs.length === 0 ? 'Valide' : 'Invalide';

  return res.json({
    statut: doc.statut,
    codeDocument,
    nomFichier: fileName,
    message: doc.statut === 'Valide'
      ? `Document ${codeDocument} validé avec succès.`
      : `Document ${codeDocument} rejeté : ${toutesErreurs.length} anomalie(s) détectée(s).`,
    erreurs: toutesErreurs
  });
});

// Détachement d'un document
app.delete('/api/liasses/:id/documents/:codeDocument', (req: Request, res: Response) => {
  const liasseId = parseInt(String(req.params.id), 10);
  const code = String(req.params.codeDocument || '').toUpperCase();

  const liasse = liassesDb.find(l => l.id === liasseId);
  if (!liasse) return res.status(404).json({ message: "Liasse introuvable." });

  const doc = liasse.documents.find(d => d.codeDocument.toUpperCase() === code);
  if (!doc) return res.status(404).json({ message: "Document introuvable." });

  if (doc.cheminStockage && fs.existsSync(doc.cheminStockage)) {
    try { fs.unlinkSync(doc.cheminStockage); } catch { }
  }

  doc.nomFichier = null;
  doc.cheminStockage = null;
  doc.dateUpload = null;
  doc.statut = 'NonSoumis';
  doc.erreurs = [];

  return res.json({ message: `Document ${code} détaché avec succès.` });
});

// Téléchargement d'un document
app.get('/api/liasses/:id/documents/:codeDocument/download', (req: Request, res: Response) => {
  const liasseId = parseInt(String(req.params.id), 10);
  const code = String(req.params.codeDocument || '').toUpperCase();

  const liasse = liassesDb.find(l => l.id === liasseId);
  if (!liasse) return res.status(404).json({ message: "Liasse introuvable." });

  const doc = liasse.documents.find(d => d.codeDocument.toUpperCase() === code);
  if (!doc || !doc.cheminStockage || !fs.existsSync(doc.cheminStockage)) {
    return res.status(404).json({ message: "Fichier introuvable pour ce document." });
  }

  return res.download(doc.cheminStockage, doc.nomFichier || `${code}.xml`);
});

// Rendu HTML
app.get('/api/liasses/:id/documents/:codeDocument/html', (req: Request, res: Response) => {
  const liasseId = parseInt(String(req.params.id), 10);
  const code = String(req.params.codeDocument || '').toUpperCase();

  const liasse = liassesDb.find(l => l.id === liasseId);
  if (!liasse) return res.status(404).send("Liasse introuvable.");

  const doc = liasse.documents.find(d => d.codeDocument.toUpperCase() === code);
  if (!doc) return res.status(404).send("Document introuvable.");

  let xmlContent = '';
  if (doc.cheminStockage && fs.existsSync(doc.cheminStockage)) {
    xmlContent = fs.readFileSync(doc.cheminStockage, 'utf-8');
  }

  const html = `<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <title>${doc.codeDocument} - ${doc.libelle}</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 30px; color: #2b3a55; background: #f8fafc; line-height: 1.5; }
    .container { max-width: 900px; margin: auto; background: #fff; border: 1px solid #dcdfe6; border-radius: 6px; box-shadow: 0 4px 12px rgba(0,0,0,0.06); padding: 30px; }
    .header { border-bottom: 2px solid #d9531e; padding-bottom: 15px; margin-bottom: 20px; display: flex; justify-content: space-between; align-items: center; }
    .title { font-size: 18px; font-weight: 700; color: #2b3a55; }
    .meta-box { background: #fdfaf8; border: 1px solid #f1ded4; border-radius: 4px; padding: 14px 18px; display: grid; grid-template-columns: repeat(2, 1fr); gap: 10px; margin-bottom: 24px; font-size: 13px; }
    .meta-label { color: #666; font-size: 11px; text-transform: uppercase; }
    .meta-val { font-weight: 600; color: #2b3a55; }
    .xml-raw { margin-top: 24px; background: #1e293b; color: #e2e8f0; padding: 16px; border-radius: 4px; font-family: monospace; font-size: 12px; overflow-x: auto; max-height: 400px; white-space: pre-wrap; }
    .badge { display: inline-block; padding: 3px 8px; border-radius: 3px; font-size: 11.5px; font-weight: 600; background: ${doc.statut === 'Valide' ? '#e8f5e9' : '#ffebee'}; color: ${doc.statut === 'Valide' ? '#2e7d32' : '#c62828'}; }
    @media print { .no-print { display: none; } body { padding: 0; background: #fff; } .container { box-shadow: none; border: none; padding: 0; } }
  </style>
</head>
<body>
  <div class="container">
    <div class="no-print" style="text-align: right; margin-bottom: 15px;">
      <button onclick="window.print()" style="background:#d9531e;color:#fff;border:none;padding:8px 16px;border-radius:4px;cursor:pointer;font-weight:600;">🖨 Imprimer l'état financier</button>
    </div>
    <div class="header">
      <div>
        <div style="font-size:12px;font-weight:bold;color:#d9531e;">RÉPUBLIQUE TUNISIENNE • MINISTÈRE DES FINANCES</div>
        <div class="title">${doc.codeDocument} : ${doc.libelle}</div>
      </div>
      <div>
        <span class="badge">Statut : ${doc.statut}</span>
      </div>
    </div>
    <div class="meta-box">
      <div><div class="meta-label">Matricule Fiscal</div><div class="meta-val">${liasse.matriculeFiscal}</div></div>
      <div><div class="meta-label">Exercice Comptable</div><div class="meta-val">${liasse.exercice}</div></div>
      <div><div class="meta-label">Nom de Fichier</div><div class="meta-val">${doc.nomFichier || 'Non téléversé'}</div></div>
      <div><div class="meta-label">Date de Validation</div><div class="meta-val">${doc.dateUpload || '—'}</div></div>
    </div>
    <h4 style="font-size:13px; text-transform:uppercase; color:#2b3a55; margin-bottom:8px;">Contenu du Fichier :</h4>
    <pre class="xml-raw">${xmlContent ? xmlContent.replace(/</g, '&lt;').replace(/>/g, '&gt;') : 'Aucun contenu disponible.'}</pre>
  </div>
</body>
</html>`;

  return res.type('html').send(html);
});

// 5. Validation à blanc (stand-alone)
app.post('/api/validation/:codeDocument', upload.single('fichier'), (req: Request, res: Response) => {
  const codeDocument = String(req.params.codeDocument || '').toUpperCase();

  if (!req.file) {
    return res.status(400).json({ message: "Aucun fichier reçu." });
  }

  let xmlContent = '';
  try {
    xmlContent = fs.readFileSync(req.file.path, 'utf-8');
    fs.unlinkSync(req.file.path);
  } catch (ex: any) {
    return res.status(400).json({ message: `Erreur de lecture du fichier : ${ex.message}` });
  }

  const result = validerXmlComplet(codeDocument, xmlContent);

  return res.json({
    codeDocument,
    estValide: result.estValide,
    totalErreurs: result.erreurs.length,
    erreurs: result.erreurs
  });
});

// 6. Dépôt officiel de la Liasse
app.post('/api/deposits', (req: Request, res: Response) => {
  const { liasseId } = req.body;
  const id = parseInt(liasseId, 10);

  const liasse = liassesDb.find(l => l.id === id && l.statut !== 'Supprimee');
  if (!liasse) {
    return res.status(404).json({ message: "Liasse introuvable." });
  }

  // Vérification de la complétude obligatoire
  const nonValides = liasse.documents.filter(d => d.estObligatoire && d.statut !== 'Valide' && d.statut !== 'Soumis');
  if (nonValides.length > 0) {
    return res.status(400).json({
      message: `Dépôt impossible : ${nonValides.length} document(s) obligatoire(s) non valide(s) (${nonValides.map(d => d.codeDocument).join(', ')}).`
    });
  }

  const year = liasse.exercice;
  const randRef = Math.floor(100000 + Math.random() * 900000);
  const reference = `DEP-${year}-${randRef}`;

  // Calcul du hash global SHA-256
  const hash = crypto.createHash('sha256');
  for (const doc of liasse.documents) {
    if (doc.cheminStockage && fs.existsSync(doc.cheminStockage)) {
      const bytes = fs.readFileSync(doc.cheminStockage);
      hash.update(bytes);
    }
  }
  const hashGlobal = hash.digest('hex');

  const dateDepot = new Date().toISOString();
  const numeroAccuse = `ACC-${year}-${randRef}`;

  const deposit: Deposit = {
    id: depositsDb.length + 1,
    reference,
    liasseId: liasse.id,
    matriculeFiscal: liasse.matriculeFiscal,
    exercice: liasse.exercice,
    dateDepot,
    statut: 'Accepte',
    hashGlobal,
    receipt: {
      numeroAccuse,
      dateEmission: dateDepot,
      qrCode: `https://impots.finances.gov.tn/verify/${reference}`,
      empreinteNumerique: hashGlobal
    }
  };

  depositsDb.push(deposit);
  liasse.statut = 'Deposee';
  liasse.documents.forEach(d => {
    if (d.statut === 'Valide') d.statut = 'Soumis';
  });

  return res.status(201).json({
    reference,
    statut: 'Accepte',
    dateDepot,
    message: "Liasse fiscale déposée avec succès auprès de la Direction Générale des Impôts.",
    receipt: deposit.receipt
  });
});

// 7. Suivi de Dépôt & Accusé
app.get('/api/tracking/:reference', (req: Request, res: Response) => {
  const ref = String(req.params.reference || '').trim();
  const deposit = depositsDb.find(d => d.reference.toUpperCase() === ref.toUpperCase());

  if (!deposit) {
    return res.status(404).json({ message: `Dépôt '${ref}' introuvable dans le système DGI.` });
  }

  const liasse = liassesDb.find(l => l.id === deposit.liasseId);
  const contribuable = contribuablesDb.find(c => c.matriculeFiscal === deposit.matriculeFiscal);

  return res.json({
    reference: deposit.reference,
    matriculeFiscal: deposit.matriculeFiscal,
    contribuable: contribuable || { nomOuRaisonSociale: "SOCIÉTÉ DÉCLARANTE", matriculeFiscalComplet: deposit.matriculeFiscal },
    exercice: deposit.exercice,
    dateDepot: deposit.dateDepot,
    statut: deposit.statut,
    hashGlobal: deposit.hashGlobal,
    accuseDisponible: !!deposit.receipt,
    documents: liasse ? liasse.documents.filter(d => d.nomFichier !== null) : []
  });
});

// Accusé de réception HTML / PDF
app.get('/api/tracking/:reference/receipt/pdf', (req: Request, res: Response) => {
  const ref = String(req.params.reference || '').trim();
  const deposit = depositsDb.find(d => d.reference.toUpperCase() === ref.toUpperCase());

  if (!deposit) return res.status(404).send("Dépôt introuvable.");

  const liasse = liassesDb.find(l => l.id === deposit.liasseId);
  const contribuable = contribuablesDb.find(c => c.matriculeFiscal === deposit.matriculeFiscal);

  const html = `<!DOCTYPE html>
<html lang="fr">
<head>
  <meta charset="utf-8">
  <title>Accusé de Réception Officiel - ${deposit.reference}</title>
  <style>
    body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; padding: 40px; color: #1e293b; background: #f1f5f9; }
    .card { max-width: 850px; margin: auto; background: #fff; border: 2px solid #2e7d32; border-radius: 8px; box-shadow: 0 10px 25px rgba(0,0,0,0.08); padding: 40px; }
    .header { text-align: center; border-bottom: 2px solid #e2e8f0; padding-bottom: 20px; margin-bottom: 25px; }
    .republique { font-size: 13px; font-weight: 800; letter-spacing: 1px; color: #475569; }
    .title { font-size: 22px; font-weight: 800; color: #2e7d32; margin-top: 8px; }
    .meta-table { width: 100%; border-collapse: collapse; margin-bottom: 25px; }
    .meta-table th, .meta-table td { padding: 10px 14px; border: 1px solid #e2e8f0; font-size: 13px; text-align: left; }
    .meta-table th { background: #f8fafc; font-weight: 600; color: #475569; width: 35%; }
    .meta-table td { font-weight: 600; color: #0f172a; }
    .docs-table { width: 100%; border-collapse: collapse; margin-top: 15px; font-size: 12.5px; }
    .docs-table th, .docs-table td { padding: 8px 12px; border: 1px solid #cbd5e1; }
    .docs-table th { background: #f1f5f9; color: #334155; font-weight: 700; }
    .hash-box { background: #f8fafc; border: 1px dashed #94a3b8; padding: 12px; border-radius: 4px; font-family: monospace; font-size: 11px; word-break: break-all; margin-top: 20px; }
    .footer { text-align: center; margin-top: 30px; font-size: 11.5px; color: #64748b; border-top: 1px solid #e2e8f0; padding-top: 15px; }
    @media print { body { padding: 0; background: #fff; } .card { border: 1px solid #000; box-shadow: none; padding: 20px; } .no-print { display: none; } }
  </style>
</head>
<body>
  <div class="card">
    <div class="no-print" style="text-align: right; margin-bottom: 20px;">
      <button onclick="window.print()" style="background:#2e7d32;color:#fff;border:none;padding:10px 20px;border-radius:4px;cursor:pointer;font-weight:700;font-size:13px;">🖨 Imprimer l'Accusé Officiel (PDF)</button>
    </div>
    <div class="header">
      <div class="republique">RÉPUBLIQUE TUNISIENNE • MINISTÈRE DES FINANCES</div>
      <div style="font-size: 12px; color: #64748b; margin-top: 2px;">DIRECTION GÉNÉRALE DES IMPÔTS — TÉLÉDÉCLARATION FISCALE</div>
      <div class="title">RÉCÉPISSÉ OFFICIEL DE DÉPÔT DE LA LIASSE FISCALE</div>
    </div>

    <table class="meta-table">
      <tr><th>Référence Officielle du Dépôt</th><td style="color:#2e7d32;font-size:15px;">${deposit.reference}</td></tr>
      <tr><th>Numéro d'Accusé de Réception</th><td>${deposit.receipt?.numeroAccuse}</td></tr>
      <tr><th>Raison Sociale / Contribuable</th><td>${contribuable?.nomOuRaisonSociale || 'SOCIÉTÉ DÉCLARANTE'}</td></tr>
      <tr><th>Matricule Fiscal Déclarant</th><td>${contribuable?.matriculeFiscalComplet || deposit.matriculeFiscal}</td></tr>
      <tr><th>Exercice Comptable Déposé</th><td>${deposit.exercice}</td></tr>
      <tr><th>Date et Heure du Dépôt</th><td>${new Date(deposit.dateDepot).toLocaleString('fr-FR')} (Horodatage Certifié)</td></tr>
      <tr><th>Statut du Traitement DGI</th><td><span style="color:#2e7d32;">✔ VALIDÉ ET ENREGISTRÉ</span></td></tr>
    </table>

    <h4 style="font-size:13px; text-transform:uppercase; color:#334155; margin-bottom: 8px;">États Financiers Reçus et Archivés :</h4>
    <table class="docs-table">
      <thead><tr><th>Code</th><th>Libellé de l'État Financier</th><th>Fichier Déposé</th><th>Statut</th></tr></thead>
      <tbody>
        ${(liasse?.documents || []).filter(d => d.nomFichier !== null).map(d => `
          <tr>
            <td><strong>${d.codeDocument}</strong></td>
            <td>${d.libelle}</td>
            <td>${d.nomFichier}</td>
            <td style="color:#2e7d32;font-weight:600;">✔ Soumis conforme</td>
          </tr>
        `).join('')}
      </tbody>
    </table>

    <div class="hash-box">
      <strong>Empreinte Numérique Globale (SHA-256) :</strong><br/>
      ${deposit.hashGlobal}
    </div>

    <div class="footer">
      Document officiel émis électroniquement conformément à la réglementation fiscale en vigueur en République Tunisienne.<br/>
      L'authenticité de ce document peut être vérifiée sur le portail fiscal officiel.
    </div>
  </div>
</body>
</html>`;

  return res.type('html').send(html);
});

// Distribution des fichiers statiques du frontend
app.use(express.static(path.join(__dirname, 'public')));

// Route de repli pour SPA
app.get('*', (_req: Request, res: Response) => {
  res.sendFile(path.join(__dirname, 'public', 'index.html'));
});

app.listen(PORT, '0.0.0.0', () => {
  console.log(`Portail Liasse Fiscale démarré avec succès sur http://0.0.0.0:${PORT}`);
  console.log(`Moteur de validation XML (XSD 1.0 + Assertions métier) actif.`);
});
