import { DefaultNamingStrategy, NamingStrategyInterface, Table } from 'typeorm';
import { snakeCase } from 'typeorm/util/StringUtils';

export class SnakeCaseNamingStrategy extends DefaultNamingStrategy implements NamingStrategyInterface {
  // protected getTableName(tableOrName: Table | string): string {
  //   return typeof tableOrName === 'string' ? tableOrName : tableOrName.name;
  // }

  public tableName(className: string, customName: string | undefined): string {
    return customName ?? snakeCase(className);
  }

  public closureJunctionTableName(originalClosureTableName: string): string {
    return snakeCase(`${originalClosureTableName}_closure_junction`);
  }

  public columnName(propertyName: string, customName: string | undefined, embeddedPrefixes: string[]): string {
    const prefix = embeddedPrefixes.length ? snakeCase(embeddedPrefixes.join('_')) + '_' : '';
    return prefix + (customName ?? snakeCase(propertyName));
  }

  public relationName(propertyName: string): string {
    return snakeCase(propertyName);
  }

  public primaryKeyName(tableOrName: Table | string, columnNames: string[]): string {
    const tableName = this.getTableName(tableOrName);
    return snakeCase(`${tableName}_${columnNames.join('_')}_primary_key`);
  }

  public uniqueConstraintName(tableOrName: Table | string, columnNames: string[]): string {
    const tableName = this.getTableName(tableOrName);
    return snakeCase(`${tableName}_${columnNames.join('_')}_unique`);
  }

  public relationConstraintName(tableOrName: Table | string, columnNames: string[], where?: string): string {
    const tableName = this.getTableName(tableOrName);
    return snakeCase(`${tableName}_${columnNames.join('_')}${where ? `_${where}` : ''}_relation`);
  }

  public defaultConstraintName(tableOrName: Table | string, columnName: string): string {
    const tableName = this.getTableName(tableOrName);
    return snakeCase(`${tableName}_${columnName}_default`);
  }

  public foreignKeyName(tableOrName: Table | string, columnNames: string[], referencedTablePath?: string, referencedColumnNames?: string[]): string {
    const tableName = this.getTableName(tableOrName);
    const parts = [tableName];

    if (referencedTablePath || (referencedColumnNames && referencedColumnNames.length)) {
      if (referencedTablePath) parts.push(referencedTablePath);
      if (referencedColumnNames && referencedColumnNames.length) parts.push(referencedColumnNames.join('_'));
      parts.push('as');
    }

    parts.push(columnNames.join('_'), 'foreign_key');

    return snakeCase(parts.join('_'));
  }

  public indexName(tableOrName: Table | string, columnNames: string[], where?: string): string {
    const tableName = this.getTableName(tableOrName);
    return snakeCase(`${tableName}_${columnNames.join('_')}${where ? `_${where}` : ''}_index`);
  }

  public checkConstraintName(tableOrName: Table | string, expression: string, isEnum?: boolean): string {
    const tableName = this.getTableName(tableOrName);
    // Aqui poderia ocorrer alguma sanitização de "expression", se necessário.
    return snakeCase(`${tableName}_${expression}${isEnum ? '_enum' : ''}_check`);
  }

  public exclusionConstraintName(tableOrName: Table | string, expression: string): string {
    const tableName = this.getTableName(tableOrName);
    return snakeCase(`${tableName}_${expression}_exclusion`);
  }

  public joinColumnName(relationName: string, referencedColumnName: string): string {
    return snakeCase(`${relationName}_${referencedColumnName}`);
  }

  public joinTableName(firstTableName: string, secondTableName: string, firstPropertyName: string, secondPropertyName: string): string {
    return snakeCase(`${firstTableName}_${firstPropertyName.replace(/\./gi, '_')}_${secondTableName}${secondPropertyName.replace(/\./gi, '_')}`);
  }

  public joinTableColumnDuplicationPrefix(columnName: string, index: number): string {
    return snakeCase(`${columnName}_${index}`);
  }

  public joinTableColumnName(tableName: string, propertyName: string, columnName?: string): string {
    return snakeCase(`${tableName}_${columnName ?? propertyName}`);
  }

  public joinTableInverseColumnName(tableName: string, propertyName: string, columnName?: string): string {
    return snakeCase(`${tableName}_${columnName ?? propertyName}`);
  }

  public prefixTableName(prefix: string, tableName: string): string {
    return prefix ? `${snakeCase(prefix)}_${snakeCase(tableName)}` : snakeCase(tableName);
  }

  public classTableInheritanceParentColumnName(parentTableName: string, parentTableIdPropertyName: string): string {
    return snakeCase(`${parentTableName}_${parentTableIdPropertyName}`);
  }
}
