import { faker } from '@faker-js/faker';

export const baseURL = 'http://localhost:8080';

export function delay(ms: number) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function generatePassword(): string {
  // Generate the required characters
  const lowercase = faker.string.alpha({ length: 1, casing: 'lower' });
  const uppercase = faker.string.alpha({ length: 1, casing: 'upper' });
  const number = faker.number.int(1);
  const symbol = faker.string.symbol(1);

  // Generate the remaining characters to make the length at least 8
  const remainingLength = 4 + Math.floor(Math.random() * 5); // to make the total length between 8 and 12
  const remainingChars = faker.string.alphanumeric(remainingLength);

  // Combine all parts and shuffle them
  const allChars = lowercase + uppercase + number + symbol + remainingChars;
  const shuffledChars = allChars
    .split('')
    .sort(() => 0.5 - Math.random())
    .join('');

  return shuffledChars;
}
