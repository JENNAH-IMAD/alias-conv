export const stats = [
  { label: 'Conversions / mois', value: '128', trend: '+18%' },
  { label: 'Taux de réussite', value: '96.4%', trend: '+2.1%' },
  { label: 'Anomalies bloquantes', value: '12', trend: '-7%' },
  { label: 'Volume traité', value: '24.8K', trend: '+12%' },
];

export const clients = [
  {
    id: 'cli-001',
    name: 'Client Alpha',
    legalName: 'Société Alpha SARL',
    ice: '00123456700015',
    if: 'IF-12345678',
    rc: 'RC 123456',
    email: 'finance@alpha.ma',
    phone: '+212 5 22 11 22 33',
    status: 'Actif',
    country: 'Maroc',
    banks: ['Banque Populaire', 'Attijariwafa Bank'],
  },
  {
    id: 'cli-002',
    name: 'Entreprise Beta',
    legalName: 'Entreprise Beta SA',
    ice: '00987654300012',
    if: 'IF-87654321',
    rc: 'RC 876543',
    email: 'compta@beta.ma',
    phone: '+212 5 22 44 55 66',
    status: 'Actif',
    country: 'Maroc',
    banks: ['Crédit Agricole', 'BMCE Bank'],
  },
  {
    id: 'cli-003',
    name: 'Sarl Gamma',
    legalName: 'Sarl Gamma',
    ice: '00333222100018',
    if: 'IF-55556666',
    rc: 'RC 555666',
    email: 'admin@gamma.ma',
    phone: '+212 5 22 88 99 00',
    status: 'En attente',
    country: 'Maroc',
    banks: ['BNP Paribas', 'Banque Populaire'],
  },
];

export const bankAccounts = [
  {
    id: 'acc-1',
    clientId: 'cli-001',
    bank: 'Banque Populaire',
    iban: 'MA64 1234 5678 9012 3456 7890 123',
    account: '000123456789',
    journal: 'BQ01',
    accountCode: '514100',
    currency: 'MAD',
  },
  {
    id: 'acc-2',
    clientId: 'cli-001',
    bank: 'Attijariwafa Bank',
    iban: 'MA64 3333 4444 5555 6666 7777 888',
    account: '000987654321',
    journal: 'AQ01',
    accountCode: '512000',
    currency: 'MAD',
  },
  {
    id: 'acc-3',
    clientId: 'cli-002',
    bank: 'Crédit Agricole',
    iban: 'MA64 2222 3333 4444 5555 6666 777',
    account: '000222233334',
    journal: 'CA01',
    accountCode: '514200',
    currency: 'MAD',
  },
];

export const conversionHistory = [
  {
    id: 'conv-101',
    client: 'Client Alpha',
    bank: 'Banque Populaire',
    period: 'Mai 2026',
    status: 'Validé',
    count: 124,
    date: '12 mai 2026',
  },
  {
    id: 'conv-102',
    client: 'Entreprise Beta',
    bank: 'Crédit Agricole',
    period: 'Avril 2026',
    status: 'En cours',
    count: 89,
    date: '02 mai 2026',
  },
  {
    id: 'conv-103',
    client: 'Sarl Gamma',
    bank: 'BNP Paribas',
    period: 'Mars 2026',
    status: 'Erreur',
    count: 46,
    date: '28 avr. 2026',
  },
  {
    id: 'conv-104',
    client: 'Client Alpha',
    bank: 'Attijariwafa Bank',
    period: 'Février 2026',
    status: 'Généré',
    count: 76,
    date: '15 avr. 2026',
  },
];

export const transactions = [
  { date: '2026-05-01', reference: 'VIR-245678', description: 'Virement client', debit: '', credit: '12500', balance: '78520' },
  { date: '2026-05-03', reference: 'CB-001', description: 'Paiement fournisseur', debit: '3240', credit: '', balance: '75280' },
  { date: '2026-05-10', reference: 'PRLV', description: 'Prélèvement électricité', debit: '1180', credit: '', balance: '74100' },
  { date: '2026-05-15', reference: 'SEPA-552', description: 'Virement interne', debit: '', credit: '8400', balance: '82500' },
  { date: '2026-05-18', reference: 'CB-013', description: 'Frais bancaires', debit: '65', credit: '', balance: '82435' },
];

export const templates = [
  {
    id: 'tpl-1',
    name: 'Sage X3 – Import Banque',
    target: 'Sage X3',
    format: 'CSV',
    delimiter: ';',
    encoding: 'UTF-8',
    fields: ['Date', 'Journal', 'Compte', 'Libellé', 'Débit', 'Crédit', 'Référence'],
  },
  {
    id: 'tpl-2',
    name: 'Sage 100 – Standard',
    target: 'Sage 100',
    format: 'TXT',
    delimiter: '\t',
    encoding: 'Windows-1252',
    fields: ['Date', 'Compte', 'Libellé', 'Montant', 'Sens', 'Analytique'],
  },
  {
    id: 'tpl-3',
    name: 'CSV personnalisé',
    target: 'Custom',
    format: 'CSV',
    delimiter: ',',
    encoding: 'UTF-8',
    fields: ['Date', 'Libellé', 'Débit', 'Crédit', 'Solde'],
  },
];

export const extractionProfiles = [
  {
    id: 'prof-1',
    bank: 'Banque Populaire',
    name: 'Profil BP Standard',
    status: 'Actif',
    mapping: ['Détection d’en-tête', 'Colonnes date + montant', 'Libellé multi-ligne'],
  },
  {
    id: 'prof-2',
    bank: 'Crédit Agricole',
    name: 'Profil CA Standard',
    status: 'Actif',
    mapping: ['Regex sur référence', 'Format de solde', 'Normalisation des chiffres'],
  },
  {
    id: 'prof-3',
    bank: 'BNP Paribas',
    name: 'Profil BNP — Test',
    status: 'À vérifier',
    mapping: ['Découpage lignes', 'Détection des montants', 'Moteur OCR fallback'],
  },
];

export const auditEntries = [
  { action: 'Import du relevé', user: 'A. Rahmani', detail: 'releve_mai_2026.pdf', time: '08:15' },
  { action: 'Validation du solde', user: 'L. Benali', detail: 'Écart 0.00 MAD', time: '08:26' },
  { action: 'Génération CSV', user: 'S. Idrissi', detail: 'Client_Alpha_SageX3_mai.csv', time: '08:41' },
  { action: 'Correction de libellé', user: 'M. Tazi', detail: 'Ligne 18', time: '09:02' },
];

export const bankRegistry = [
  { name: 'Banque Populaire', clients: 11, status: 'OK' },
  { name: 'Crédit Agricole', clients: 8, status: 'OK' },
  { name: 'BNP Paribas', clients: 6, status: 'À vérifier' },
  { name: 'Attijariwafa Bank', clients: 9, status: 'OK' },
  { name: 'BMCE Bank', clients: 5, status: 'OK' },
  { name: 'SG', clients: 4, status: 'OK' },
];

export const settings = [
  { label: 'Rétention des fichiers', value: '180 jours' },
  { label: 'Encodage par défaut', value: 'UTF-8' },
  { label: 'Format export', value: 'CSV + XLSX' },
  { label: 'Sécurité', value: 'RLS activé' },
];
