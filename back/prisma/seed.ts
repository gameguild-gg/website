import { Prisma, PrismaClient, Role } from '@prisma/client';

const prisma = new PrismaClient();

async function main() {
  const superAdmin: Prisma.UserCreateInput = {
    email: 'superAdmin@superAdmin.com',
    role: Role.ADMIN,
    passwordHash:
      '0x298e1015392434fb789f8dcf0a9d700d803bb7e3c643d7cdbc11bdfaa3db2835',
    passwordSalt:
      '0xd3cbdbd5100ca77a6606b56e20959a0914c2fbb142f01357b93223f0e276c6f7',
  };

  await prisma.user.create({ data: superAdmin });
}

main()
  .catch((e) => {
    console.error(e);
    process.exit(1);
  })
  .finally(async () => {
    await prisma.$disconnect();
  });
