// Palettes inspired by the banks' visual identities. They identify bank accounts,
// without implying that a particular payment-card product was issued.
const bankThemes = [
  {
    key: 'attijari',
    label: 'Attijariwafa',
    aliases: ['attijariwafa bank', 'attijariwafa', 'awb', 'att'],
    background: 'linear-gradient(125deg, #ffcf45, #f5a623 58%, #e57216)',
    ink: '#30231a',
  },
  {
    key: 'barid',
    label: 'Al Barid',
    aliases: ['al barid bank', 'al barid', 'abb'],
    background: 'linear-gradient(125deg, #ffe16a, #f5cd21 65%, #d5a512)',
    ink: '#40352c',
  },
  {
    key: 'populaire',
    label: 'Banque Populaire',
    aliases: [
      'banque populaire',
      'banque centrale populaire',
      'credit populaire du maroc',
      'bcp',
      'bp',
    ],
    background: 'linear-gradient(125deg, #b95c22, #88401f 65%, #542c20)',
    ink: '#ffffff',
  },
  {
    key: 'boa',
    label: 'BANK OF AFRICA',
    aliases: [
      'bank of africa',
      'bmce bank',
      'bmce',
      'boa',
      'banque marocaine du commerce exterieur',
    ],
    background: 'linear-gradient(125deg, #1689c0, #00539b 58%, #003568)',
    ink: '#ffffff',
  },
  {
    key: 'bmci',
    label: 'BMCI',
    aliases: [
      'bmci',
      'banque marocaine pour le commerce et industrie',
      'banque marocaine pour le commerce et l industrie',
    ],
    background: 'linear-gradient(125deg, #28ad85, #008263 55%, #004e40)',
    ink: '#ffffff',
  },
  {
    key: 'agricole',
    label: 'Crédit Agricole',
    aliases: ['credit agricole du maroc', 'credit agricole', 'cam', 'ca'],
    background: 'linear-gradient(125deg, #49a158, #147439 55%, #06472c)',
    ink: '#ffffff',
  },
  {
    key: 'cfg',
    label: 'CFG Bank',
    aliases: ['cfg bank', 'cfg'],
    background: 'linear-gradient(125deg, #f45447, #d62e2c 60%, #9e2025)',
    ink: '#ffffff',
  },
  {
    key: 'cih',
    label: 'CIH BANK',
    aliases: ['cih bank', 'cih', 'credit immobilier et hotelier'],
    background: 'linear-gradient(125deg, #007fb5, #005880 76%, #ed7623 76%, #f49b37)',
    ink: '#ffffff',
  },
  {
    key: 'cdm',
    label: 'Crédit du Maroc',
    aliases: ['credit du maroc', 'cdm'],
    background: 'linear-gradient(125deg, #184e98, #10316b 78%, #c72b41 78%, #a31e35)',
    ink: '#ffffff',
  },
  {
    key: 'saham',
    label: 'SAHAM BANK',
    aliases: ['saham bank', 'saham', 'sb'],
    background: 'linear-gradient(125deg, #e27142, #c24e29 58%, #873a24)',
    ink: '#ffffff',
  },
  {
    key: 'sg',
    label: 'Société Générale',
    aliases: ['societe generale maroc', 'societe generale', 'sgmb', 'sgma', 'sg'],
    background: 'linear-gradient(155deg, #e5093b 0%, #c9052c 47%, #2c2730 48%, #13141a 100%)',
    ink: '#ffffff',
  },
  {
    key: 'cdg',
    label: 'CDG Capital',
    aliases: ['cdg capital', 'cdg'],
    background: 'linear-gradient(125deg, #91ab42, #587a26 60%, #354d20)',
    ink: '#ffffff',
  },
  {
    key: 'arab',
    label: 'Arab Bank',
    aliases: ['arab bank maroc', 'arab bank', 'arab'],
    background: 'linear-gradient(125deg, #627c9d, #20446e 60%, #182d4b)',
    ink: '#ffffff',
  },
  {
    key: 'caixa',
    label: 'CaixaBank',
    aliases: ['caixabank', 'caixa bank', 'caixa'],
    background: 'linear-gradient(125deg, #46bcdf, #079aca 60%, #076786)',
    ink: '#ffffff',
  },
  {
    key: 'citi',
    label: 'Citi',
    aliases: ['citibank maghreb', 'citibank', 'citi bank', 'citi'],
    background: 'linear-gradient(125deg, #29b4dc, #076cb5 60%, #064274)',
    ink: '#ffffff',
  },
  {
    key: 'sabadell',
    label: 'Sabadell',
    aliases: ['banco sabadell', 'sabadell', 'sabadel'],
    background: 'linear-gradient(125deg, #20a6d1, #007ba7 60%, #004d70)',
    ink: '#ffffff',
  },
  {
    key: 'umb',
    label: 'UMB',
    aliases: ['union marocaine des banques', 'union marocaine de banques', 'umb'],
    background: 'linear-gradient(125deg, #7c9ed5, #3f65a3 60%, #2a437b)',
    ink: '#ffffff',
  },
  {
    key: 'assafa',
    label: 'Bank Assafa',
    aliases: ['bank assafa', 'banque assafa', 'assafa'],
    background: 'linear-gradient(125deg, #34ba94, #009679 60%, #00604f)',
    ink: '#ffffff',
  },
  {
    key: 'akhdar',
    label: 'Al Akhdar',
    aliases: ['al akhdar bank', 'al akhdar', 'akhdar'],
    background: 'linear-gradient(125deg, #60a34a, #21652b 60%, #16441f)',
    ink: '#ffffff',
  },
  {
    key: 'karam',
    label: 'Bank Al Karam',
    aliases: ['bank al karam', 'al karam', 'alkaram'],
    background: 'linear-gradient(125deg, #42c6ce, #00a7b3 60%, #006d81)',
    ink: '#ffffff',
  },
  {
    key: 'yousr',
    label: 'Bank Al Yousr',
    aliases: ['bank al yousr', 'al yousr', 'alyousr'],
    background: 'linear-gradient(125deg, #e34d51, #c72337 60%, #8c1429)',
    ink: '#ffffff',
  },
  {
    key: 'umnia',
    label: 'Umnia Bank',
    aliases: ['umnia bank', 'umnia'],
    background: 'linear-gradient(125deg, #c79448, #9c153c 62%, #6b1030)',
    ink: '#ffffff',
  },
];

export function bankCardTheme(bankName: string) {
  const normalized = bankName
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, ' ')
    .trim();
  const padded = ` ${normalized} `;
  // Prefer a complete name over a short code such as CA or BP in a longer label.
  // Unknown banks keep a neutral skin; account order never determines the colour.
  const matches = bankThemes.flatMap((theme) =>
    theme.aliases
      .filter(
        (alias) => alias === normalized || (alias.length > 3 && padded.includes(` ${alias} `)),
      )
      .map((alias) => ({
        theme,
        score: alias === normalized ? 1000 + alias.length : alias.length,
      })),
  );
  matches.sort((a, b) => b.score - a.score);
  return (
    matches[0]?.theme ?? {
      key: 'neutral',
      label: bankName,
      background: 'linear-gradient(125deg, #546b80, #293f53)',
      ink: '#ffffff',
    }
  );
}
