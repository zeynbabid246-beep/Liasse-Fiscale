import { generateAllRules } from '../src/utils/rulesGenerator.js';

try {
  console.log('--- Génération des règles métier depuis les schémas XSD officiels ---');
  const summary = generateAllRules();
  console.log('Résultats de la génération :');
  for (const [form, count] of Object.entries(summary)) {
    console.log(`  - ${form} : ${count} règles extraites`);
  }
  console.log('Génération terminée avec succès !');
} catch (err) {
  console.error('Erreur lors de la génération des règles :', err);
}
