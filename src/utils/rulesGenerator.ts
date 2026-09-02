import fs from 'fs';
import path from 'path';
import { XMLParser } from 'fast-xml-parser';

export interface RuleOperand {
  code: string;
  sign: '+' | '-';
}

export interface BusinessRule {
  target: string;
  label?: string;
  formulaRaw: string;
  operands: RuleOperand[];
  type: 'arithmetic';
}

export interface FormRulesFile {
  formCode: string;
  generatedAt: string;
  rulesCount: number;
  rules: BusinessRule[];
}

/**
 * Nettoie et extrait une formule arithmétique depuis la documentation XSD
 */
function parseFormulaFromDoc(docText: string, targetTag: string): BusinessRule | null {
  if (!docText || typeof docText !== 'string') return null;
  const trimmedDoc = docText.trim();

  // Recherche de formules du type "CODE1" + "CODE2" ou "CODE1" - "CODE2"
  // Ex: "F60010002" + "F60010031" ou "60030004" - "60030005"
  const formulaMatch = trimmedDoc.match(/(?:^|\.\s*|\:\s*)(["'\w\d_ -+\(\)]+(?:\+|\-)["'\w\d_ -+\(\)]+.*)$/);
  if (!formulaMatch) return null;

  const formulaStr = formulaMatch[1].trim();
  
  let label = trimmedDoc;
  const dotIdx = trimmedDoc.indexOf(formulaStr);
  if (dotIdx > 0) {
    label = trimmedDoc.substring(0, dotIdx).replace(/[\.\:\-\s]+$/, '').trim();
  }

  const tokenRegex = /([+-]?)\s*["']?([A-Za-z0-9_]+)["']?/g;
  const operands: RuleOperand[] = [];
  let tokenMatch;

  while ((tokenMatch = tokenRegex.exec(formulaStr)) !== null) {
    const signStr = tokenMatch[1] || '+';
    let rawCode = tokenMatch[2].trim();
    
    // Si le code ne commence pas par F mais a 8 chiffres (ex: 60030004 -> F60030004)
    if (!rawCode.startsWith('F') && /^\d{8}$/.test(rawCode)) {
      rawCode = `F${rawCode}`;
    }

    if (/^F?\d{8}$/.test(rawCode) || /^[A-Z]\d{4}\d{4}$/.test(rawCode)) {
      operands.push({
        code: rawCode.startsWith('F') ? rawCode : `F${rawCode}`,
        sign: signStr === '-' ? '-' : '+'
      });
    }
  }

  // Vérifier qu'on a bien au moins 2 opérandes valides et qu'on ne boucle pas sur soi-même
  if (operands.length >= 2 && !operands.some(op => op.code === targetTag)) {
    return {
      target: targetTag,
      label: label || undefined,
      formulaRaw: formulaStr,
      operands,
      type: 'arithmetic'
    };
  }

  return null;
}

/**
 * Analyse et extrait avec précaution les règles de calcul
 * depuis l'arborescence XML XSD
 */
export function extractRulesFromXsd(xsdContent: string, defaultPrefix: string): BusinessRule[] {
  const parser = new XMLParser({
    ignoreAttributes: false,
    attributeNamePrefix: '@_',
    textNodeName: '#text',
    removeNSPrefix: true
  });

  const parsed = parser.parse(xsdContent);
  const rules: BusinessRule[] = [];

  function traverse(node: any) {
    if (!node || typeof node !== 'object') return;

    if (node.element) {
      const elements = Array.isArray(node.element) ? node.element : [node.element];
      for (const el of elements) {
        const elemName = el['@_name'];
        if (elemName && el.annotation && el.annotation.documentation) {
          const doc = typeof el.annotation.documentation === 'string'
            ? el.annotation.documentation
            : (el.annotation.documentation['#text'] || '');
          
          const rule = parseFormulaFromDoc(doc, elemName);
          if (rule) {
            rules.push(rule);
          }
        }
        traverse(el);
      }
    }

    for (const key of Object.keys(node)) {
      if (key !== 'element' && typeof node[key] === 'object') {
        traverse(node[key]);
      }
    }
  }

  traverse(parsed);
  return rules;
}

/**
 * Génère l'ensemble des fichiers de règles JSON dans SchemaAssets/rules
 */
export function generateAllRules(): Record<string, number> {
  const xsdDirs = [
    path.join(process.cwd(), 'SchemaAssets', 'XSD- Liasse fiscale'),
    path.join(process.cwd(), 'SchemaAssets', 'XSD - Liasse fiscale'),
    path.join(process.cwd(), 'SchemaAssets')
  ];

  let xsdDir = '';
  for (const d of xsdDirs) {
    if (fs.existsSync(d) && fs.readdirSync(d).some(f => f.endsWith('.xsd'))) {
      xsdDir = d;
      break;
    }
  }

  if (!xsdDir) {
    throw new Error('Dossier XSD introuvable.');
  }

  const outputDir = path.join(process.cwd(), 'SchemaAssets', 'rules');
  if (!fs.existsSync(outputDir)) {
    fs.mkdirSync(outputDir, { recursive: true });
  }

  const results: Record<string, number> = {};
  const files = fs.readdirSync(xsdDir).filter(f => f.endsWith('.xsd') && !['Entete.xsd', 'Typescommuns.xsd'].includes(f));

  for (const file of files) {
    const formCode = path.basename(file, '.xsd');
    const xsdPath = path.join(xsdDir, file);
    const content = fs.readFileSync(xsdPath, 'utf8');

    const rules = extractRulesFromXsd(content, formCode);
    const formRules: FormRulesFile = {
      formCode,
      generatedAt: new Date().toISOString(),
      rulesCount: rules.length,
      rules
    };

    const outPath = path.join(outputDir, `${formCode}.rules.json`);
    fs.writeFileSync(outPath, JSON.stringify(formRules, null, 2), 'utf8');
    results[formCode] = rules.length;
  }

  return results;
}

