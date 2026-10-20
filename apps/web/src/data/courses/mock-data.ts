import { Product, ProductProgram, Program, ProgramContent } from '@/lib/api/generated';
import { ai4gamesProduct, ai4gamesProductProgram, ai4gamesProgram } from './ai4games';
import { ai4games2Product, ai4games2ProductProgram, ai4games2Program } from './ai4games2';
import { dataanalysisProduct, dataanalysisProductProgram, dataanalysisProgram } from './dataanalysis';
import databasesProgram, { databasesProduct, databasesProductProgram } from './databases';
import { dsaProduct, dsaProductProgram, dsaProgram } from './dsa';
import gamePublishingProgram, { gamePublishingProduct, gamePublishingProductProgram } from './game-publishing';
import intro2gproProgram, { intro2gproProduct, intro2gproProductProgram } from './intro2gpro';
import networkingProgram, { networkingProduct, networkingProductProgram } from './networking';
import portfolioProgram, { portfolioProduct, portfolioProductProgram } from './portfolio';
import { pythonProduct, pythonProductProgram, pythonProgram } from './python';

pythonProduct.productPrograms = [pythonProductProgram];
ai4gamesProduct.productPrograms = [ai4gamesProductProgram];
ai4games2Product.productPrograms = [ai4games2ProductProgram];
databasesProduct.productPrograms = [databasesProductProgram];
networkingProduct.productPrograms = [networkingProductProgram];
dsaProduct.productPrograms = [dsaProductProgram];
dataanalysisProduct.productPrograms = [dataanalysisProductProgram];

export const mockPrograms: Program[] = [
  ai4games2Program,
  networkingProgram,
  databasesProgram,
  pythonProgram,
  ai4gamesProgram,
  portfolioProgram,
  dsaProgram,
  intro2gproProgram,
  gamePublishingProgram,
  dataanalysisProgram,
];

export const mockProducts: Product[] = [
  ai4games2Product,
  networkingProduct,
  databasesProduct,
  pythonProduct,
  ai4gamesProduct,
  portfolioProduct,
  dsaProduct,
  intro2gproProduct,
  gamePublishingProduct,
  dataanalysisProduct,
];

export const mockProductPrograms: ProductProgram[] = [
  ai4games2ProductProgram,
  networkingProductProgram,
  databasesProductProgram,
  pythonProductProgram,
  ai4gamesProductProgram,
  portfolioProductProgram,
  dsaProductProgram,
  intro2gproProductProgram,
  gamePublishingProductProgram,
  dataanalysisProductProgram,
];

export const mockProgramContents: ProgramContent[] = [
  ...(ai4games2Program.programContents ?? []),
  ...(networkingProgram.programContents ?? []),
  ...(databasesProgram.programContents ?? []),
  ...(pythonProgram.programContents ?? []),
  ...(ai4gamesProgram.programContents ?? []),
  ...(portfolioProgram.programContents ?? []),
  ...(dsaProgram.programContents ?? []),
  ...(intro2gproProgram.programContents ?? []),
  ...(gamePublishingProgram.programContents ?? []),
  ...(dataanalysisProgram.programContents ?? []),
];

export function getProgramBySlug(slug: string): Program | null {
  return mockPrograms.find(program => program.slug === slug) || null;
}

export function getProductBySlug(slug: string): Product | null {
  return mockProducts.find(product => product.slug === slug) || null;
}

export function getAllPrograms(): Program[] {
  return mockPrograms;
}

export function getAllProducts(): Product[] {
  return mockProducts;
}

export function getProgramContentBySlug(programSlug: string, contentPath: string[]): ProgramContent | null {
  const program = getProgramBySlug(programSlug);
  if (!program) {
    console.log('🔍 Program not found:', programSlug);
    return null;
  }

  console.log('🔍 Searching for content:', {
    programSlug,
    contentPath,
    programContentsCount: program.programContents?.length,
  });
  console.log(
    '🔍 Available content:',
    program.programContents?.map((item) => ({ id: item.id, title: item.title, parent: item.parent, parentId: item.parentId }))
  );

  let content: ProgramContent | null =
    program.programContents?.find(
      (item) => (item.parent === null || item.parent === undefined) && item.slug === contentPath[0]
    ) || null;

  console.log('🔍 First level search result:', content ? { id: content.id, title: content.title } : 'Not found');

  if (contentPath.length > 1) {
    for (const slug of contentPath.slice(1)) {
      content = content?.children?.find((item) => item.slug === slug) || null;
      if (!content) {
        return null;
      }
    }
  }
  return content;
}
