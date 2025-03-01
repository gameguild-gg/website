import { dataSource } from './ormconfig'; // Import the dataSource from your ormconfig
import { writeFileSync } from 'fs';
import { ColumnType } from 'typeorm';

// registry of tables is a dictionary of dictionary, where the letfmost key is the table name which maps to a dictionary of field names to field type. e.g., {"UserEntity": { "id": "uuid", "username": "varchar" }}
const TableRegistry: { [key: string]: { [key: string]: string } } = {};

// registry of relations is a dictionary of dictionary, where the letfmost key is the origin which maps to a dictionary of destination to relation types. e.g., {"UserEntity.profile": { "UserProfileEntity.user": "one-to-one" }, "UserProfileEntity.user": { "UserEntity.profile": "one-to-one" }}
const RelationRegistry: { [key: string]: { [key: string]: string } } = {};

async function generateDbml() {
  // Initialize the data source (it should be initialized before running the script)
  await dataSource.initialize();

  const entities = dataSource.entityMetadatas; // Use entity metadata from the initialized data source

  // sort entities by name
  entities.sort((a, b) => a.name.localeCompare(b.name));

  // Generate tables with columns
  entities.forEach((entity) => {
    // if the entity is not in the registry, add it
    if (!TableRegistry[entity.name]) {
      TableRegistry[entity.name] = {};
    }

    // sort columns by name
    entity.columns.sort((a, b) => a.propertyName.localeCompare(b.propertyName));

    entity.columns.forEach((column) => {
      let columnType: ColumnType = column.type;

      // Check if the column is an enum
      if (column.enum) {
        columnType = 'enum';
      } else if (typeof columnType === 'function') {
        // Handle function types (e.g., String, Number)
        columnType = columnType.name.toLowerCase() as ColumnType;
      }

      // Add the column to the registry
      TableRegistry[entity.name][column.propertyName] = columnType.toString();
    });

    // sort relations by name
    entity.relations.sort((a, b) =>
      a.propertyName.localeCompare(b.propertyName),
    );

    // Ensure all relations are included as fields with type of the related entity
    // if it is an array, add [] to the type
    entity.relations.forEach((relation) => {
      TableRegistry[entity.name][relation.propertyName] =
        relation.inverseEntityMetadata.name;
    });
  });

  // Generate relationships
  entities.forEach((entity) => {
    entity.relations.forEach((relation) => {
      const from = entity.name;
      const to = relation.inverseEntityMetadata.name;
      const relationship = relation.relationType;

      let relationSymbol: string;
      switch (relationship) {
        case 'one-to-one':
          relationSymbol = '-';
          break;
        case 'one-to-many':
          relationSymbol = '<';
          break;
        case 'many-to-one':
          relationSymbol = '>';
          break;
        case 'many-to-many':
          relationSymbol = '<>';
          break;
        default:
          relationSymbol = '';
      }

      if (!relation.inverseSidePropertyPath) {
        relation.inverseSidePropertyPath = 'id';
      }

      const fromKey = `${from}.${relation.propertyName}`;
      const toKey = `${to}.${relation.inverseSidePropertyPath}`;

      // ensure the oposite relation does not exist
      if (
        !RelationRegistry[toKey] ||
        (RelationRegistry[toKey] && !RelationRegistry[toKey][fromKey])
      ) {
        if (!RelationRegistry[fromKey]) {
          RelationRegistry[fromKey] = {};
        }
        RelationRegistry[fromKey][toKey] = relationSymbol;
      }
    });
  });

  // create dbml string
  let dbml = '';

  // add tables
  for (const [tableName, columns] of Object.entries(TableRegistry)) {
    dbml += `Table ${tableName} {\n`;
    for (const [columnName, columnType] of Object.entries(columns)) {
      dbml += `  ${columnName} ${columnType}\n`;
    }
    dbml += '}\n\n';
  }

  // add relations
  for (const [from, toRelations] of Object.entries(RelationRegistry)) {
    for (const [to, relationType] of Object.entries(toRelations)) {
      dbml += `Ref: ${from} ${relationType} ${to}\n`;
    }
  }

  // Write DBML to file
  writeFileSync('./dbml-schema.dbml', dbml);
  console.log('DBML file generated.');
}

generateDbml().catch((error) => console.error('Error generating DBML:', error));
