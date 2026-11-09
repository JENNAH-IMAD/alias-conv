// Accounting is an output workflow; its source PDF remains a bank statement in the API.
export type ConversionIntent = 'STATEMENT' | 'EXTRACT' | 'ACCOUNTING';
