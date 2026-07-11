targetScope = 'subscription'

param resourceGroupName string

param location string

param principalId string

resource rg 'Microsoft.Resources/resourceGroups@2023-07-01' = {
  name: resourceGroupName
  location: location
}

module sql 'sql/sql.bicep' = {
  name: 'sql'
  scope: rg
  params: {
    location: location
  }
}

module sql_roles 'sql-roles/sql-roles.bicep' = {
  name: 'sql-roles'
  scope: rg
  params: {
    location: location
    sql_outputs_name: sql.outputs.name
    sql_outputs_sqlserveradminname: sql.outputs.sqlServerAdminName
    principalId: principalId
    principalName: ''
    principalType: ''
  }
}