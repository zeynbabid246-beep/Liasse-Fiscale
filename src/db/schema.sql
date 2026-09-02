-- =============================================================================
-- SCHÉMA DE BASE DE DONNÉES : LIASSE FISCALE (POSTGRESQL / PGADMIN)
-- =============================================================================

-- 1. Table des utilisateurs & télé-déclarants
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    matricule_fiscal VARCHAR(20) UNIQUE NOT NULL,
    raison_sociale VARCHAR(255) NOT NULL,
    role VARCHAR(50) DEFAULT 'DECLARANT', -- 'DECLARANT' ou 'ADMIN'
    annee_exercice INTEGER NOT NULL DEFAULT 2026,
    code_systeme VARCHAR(50) NOT NULL DEFAULT 'SYSTEME_NORMAL',
    modele VARCHAR(50) DEFAULT 'MODELE_NORMAL',
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 2. Table des dossiers de dépôts de Liasse Fiscale
CREATE TABLE IF NOT EXISTS deposits (
    id VARCHAR(50) PRIMARY KEY,
    matricule_fiscal VARCHAR(20) NOT NULL,
    raison_sociale VARCHAR(255) NOT NULL,
    annee_exercice INTEGER NOT NULL,
    code_systeme VARCHAR(50) NOT NULL,
    modele VARCHAR(50) NOT NULL,
    statut VARCHAR(50) NOT NULL DEFAULT 'BROUILLON', -- 'BROUILLON', 'VALIDE', 'REJETE', 'DEPOSE'
    date_depot TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    quittance_numero VARCHAR(50),
    quittance_path VARCHAR(500),
    erreurs_count INTEGER DEFAULT 0,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 3. Table des fichiers déposés (XML formulaires et PDF rapports)
CREATE TABLE IF NOT EXISTS deposit_files (
    id SERIAL PRIMARY KEY,
    deposit_id VARCHAR(50) NOT NULL REFERENCES deposits(id) ON DELETE CASCADE,
    code_document VARCHAR(20) NOT NULL,
    nom_fichier_original VARCHAR(255) NOT NULL,
    file_path VARCHAR(500) NOT NULL,
    file_size_bytes BIGINT,
    mime_type VARCHAR(100),
    statut_validation VARCHAR(50) DEFAULT 'EN_ATTENTE', -- 'VALIDE', 'ERREUR', 'EN_ATTENTE'
    rapport_validation JSONB,
    uploaded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- 4. Table des soldes et détails déclarés (rubriques comptables)
CREATE TABLE IF NOT EXISTS declaration_details (
    id SERIAL PRIMARY KEY,
    deposit_id VARCHAR(50) NOT NULL REFERENCES deposits(id) ON DELETE CASCADE,
    code_document VARCHAR(20) NOT NULL,
    code_rubrique VARCHAR(50) NOT NULL,
    valeur_declaree NUMERIC(18, 3) NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT unique_deposit_doc_rubrique UNIQUE (deposit_id, code_document, code_rubrique)
);

-- 5. Table d'audit et traçabilité
CREATE TABLE IF NOT EXISTS audit_logs (
    id SERIAL PRIMARY KEY,
    deposit_id VARCHAR(50),
    matricule_fiscal VARCHAR(20) NOT NULL,
    action VARCHAR(100) NOT NULL,
    details TEXT,
    ip_address VARCHAR(45),
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- Index pour optimiser les performances de recherche
CREATE INDEX IF NOT EXISTS idx_deposits_matricule ON deposits(matricule_fiscal);
CREATE INDEX IF NOT EXISTS idx_deposits_exercice ON deposits(annee_exercice);
CREATE INDEX IF NOT EXISTS idx_files_deposit ON deposit_files(deposit_id);
CREATE INDEX IF NOT EXISTS idx_audit_matricule ON audit_logs(matricule_fiscal);

-- Données initiales d'exemples
INSERT INTO users (matricule_fiscal, raison_sociale, role, annee_exercice, code_systeme, modele)
VALUES 
('0000121J', 'SOCIETE EXEMPLE INDUSTRIE SARL', 'DECLARANT', 2026, 'SYSTEME_NORMAL', 'MODELE_NORMAL'),
('1234567A', 'COMMERCE INTERNATIONAL TUNISIE', 'DECLARANT', 2024, 'SYSTEME_NORMAL', 'MODELE_NORMAL'),
('ADMIN001', 'DIRECTION GÉNÉRALE DES IMPÔTS', 'ADMIN', 2026, 'SYSTEME_NORMAL', 'MODELE_NORMAL')
ON CONFLICT (matricule_fiscal) DO NOTHING;

-- Exemple de dépôt de liasse fiscale
INSERT INTO deposits (id, matricule_fiscal, raison_sociale, annee_exercice, code_systeme, modele, statut, quittance_numero, quittance_path, erreurs_count)
VALUES 
('DEP-2026-0000121J', '0000121J', 'SOCIETE EXEMPLE INDUSTRIE SARL', 2026, 'SYSTEME_NORMAL', 'MODELE_NORMAL', 'Validée', 'QUIT-2026-0000121J-9921', '/uploads/quittances/QUIT-2026-0000121J.pdf', 0),
('DEP-2024-1234567A', '1234567A', 'COMMERCE INTERNATIONAL TUNISIE', 2024, 'SYSTEME_NORMAL', 'MODELE_NORMAL', 'Soumis', 'QUIT-2024-1234567A-1402', '/uploads/quittances/QUIT-2024-1234567A.pdf', 0)
ON CONFLICT (id) DO NOTHING;

-- Exemple de fichiers attachés (XML et PDF)
INSERT INTO deposit_files (deposit_id, code_document, nom_fichier_original, file_path, file_size_bytes, mime_type, statut_validation, rapport_validation)
VALUES 
('DEP-2026-0000121J', 'F6001', 'F6001-0000121J-2026.xml', '/uploads/xml/F6001-0000121J-2026.xml', 14250, 'text/xml', 'Valide', '[]'::jsonb),
('DEP-2026-0000121J', 'F6002', 'F6002-0000121J-2026.xml', '/uploads/xml/F6002-0000121J-2026.xml', 8720, 'text/xml', 'Valide', '[]'::jsonb),
('DEP-2026-0000121J', 'F6003', 'F6003-0000121J-2026.xml', '/uploads/xml/F6003-0000121J-2026.xml', 11340, 'text/xml', 'Valide', '[]'::jsonb),
('DEP-2026-0000121J', 'F6019', 'Rapport-Commissaire-2026.pdf', '/uploads/pdf/Rapport-Commissaire-2026.pdf', 458900, 'application/pdf', 'Valide', '[]'::jsonb)
ON CONFLICT DO NOTHING;

-- Exemple de rubriques comptables déclarées
INSERT INTO declaration_details (deposit_id, code_document, code_rubrique, valeur_declaree)
VALUES 
('DEP-2026-0000121J', 'F6001', 'F60010001', 545000.000),
('DEP-2026-0000121J', 'F6001', 'F60010002', 320000.000),
('DEP-2026-0000121J', 'F6001', 'F60010031', 225000.000),
('DEP-2026-0000121J', 'F6002', 'F60020001', 545000.000),
('DEP-2026-0000121J', 'F6002', 'F60020002', 280000.000),
('DEP-2026-0000121J', 'F6003', 'F60030001', 1250000.000),
('DEP-2026-0000121J', 'F6003', 'F60030040', 145000.000)
ON CONFLICT (deposit_id, code_document, code_rubrique) DO NOTHING;

-- Exemple de logs d'audit
INSERT INTO audit_logs (deposit_id, matricule_fiscal, action, details, ip_address)
VALUES 
('DEP-2026-0000121J', '0000121J', 'LOGIN', 'Connexion réussie au portail', '127.0.0.1'),
('DEP-2026-0000121J', '0000121J', 'VALIDATION_XML_SUCCES', 'Validation réussie de F6001 (Actif)', '127.0.0.1'),
('DEP-2026-0000121J', '0000121J', 'VALIDATION_XML_SUCCES', 'Validation réussie de F6002 (Passif)', '127.0.0.1'),
('DEP-2026-0000121J', '0000121J', 'DEPOT_SOUMIS', 'Liasse fiscale 2026 transmise avec succès', '127.0.0.1'),
('DEP-2026-0000121J', 'ADMIN001', 'DEPOT_VALIDE_ADMIN', 'Validation finale et délivrance de la quittance', '192.168.1.10');
