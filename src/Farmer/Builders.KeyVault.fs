﻿[<AutoOpen>]
module Farmer.Resources.KeyVault

open Farmer
open System

type [<RequireQualifiedAccess>] Key = Encrypt | Decrypt | WrapKey | UnwrapKey | Sign | Verify | Get | List | Create | Update | Import | Delete | Backup | Restore | Recover | Purge
type [<RequireQualifiedAccess>] Secret = Get | List | Set | Delete | Backup | Restore | Recover | Purge
type [<RequireQualifiedAccess>] Certificate = Get | List | Delete | Create | Import | Update | ManageContacts | GetIssuers | ListIssuers | SetIssuers | DeleteIssuers | ManageIssuers | Recover | Purge | Backup | Restore
type [<RequireQualifiedAccess>] Storage = Get | List | Delete | Set | Update | RegenerateKey | Recover | Purge | Backup | Restore | SetSas | ListSas | GetSas | DeleteSas

type AccessPolicy =
    { ObjectId : string
      ApplicationId : Guid option
      Permissions :
        {| Keys : Key Set
           Secrets : Secret Set
           Certificates : Certificate Set
           Storage : Storage Set |}
    }
type NonEmptyList<'T> = 'T * 'T List

type CreateMode = Recover of NonEmptyList<AccessPolicy> | Default of AccessPolicy list | Unspecified of AccessPolicy list
type SoftDeletionMode = SoftDeleteWithPurgeProtection | SoftDeletionOnly
type KeyVaultSku = Standard | Premium
type KeyVaultSettings =
    { /// Specifies whether Azure Virtual Machines are permitted to retrieve certificates stored as secrets from the key vault.
      VirtualMachineAccess : FeatureFlag option
      /// Specifies whether Azure Resource Manager is permitted to retrieve secrets from the key vault.
      ResourceManagerAccess : FeatureFlag option
      /// Specifies whether Azure Disk Encryption is permitted to retrieve secrets from the vault and unwrap keys.
      AzureDiskEncryptionAccess : FeatureFlag option
      /// Specifies whether Soft Deletion is enabled for the vault
      SoftDelete : SoftDeletionMode option }
type KeyVaultConfig =
    { Name : ResourceName
      Access : KeyVaultSettings
      Sku : KeyVaultSku
      Policies : CreateMode
      /// Specifies the Azure Active Directory tenant ID that should be used for authenticating requests to the key vault.
      TenantId : Guid
      Uri : Uri option }

type AccessPolicyBuilder() =
    member __.Yield _ =
        { ObjectId = null
          ApplicationId = None
          Permissions = {| Keys = Set.empty; Secrets = Set.empty; Certificates = Set.empty; Storage = Set.empty |} }
    /// Sets the Object ID of the permission set.
    [<CustomOperation "object_id">]
    member __.ObjectId(state:AccessPolicy, objectId) = { state with ObjectId = objectId }
    /// Sets the Application ID of the permission set.
    [<CustomOperation "application_id">]
    member __.ApplicationId(state:AccessPolicy, applicationId) = { state with ApplicationId = Some applicationId }
    /// Sets the Key permissions of the permission set.
    [<CustomOperation "key_permissions">]
    member __.SetKeyPermissions(state:AccessPolicy, permissions) = { state with Permissions = {| state.Permissions with Keys = Set permissions |} }
    /// Sets the Storage permissions of the permission set.
    [<CustomOperation "storage_permissions">]
    member __.SetStoragePermissions(state:AccessPolicy, permissions) = { state with Permissions = {| state.Permissions with Storage = Set permissions |} }
    /// Sets the Secret permissions of the permission set.
    [<CustomOperation "secret_permissions">]
    member __.SetSecretPermissions(state:AccessPolicy, permissions) = { state with Permissions = {| state.Permissions with Secrets = Set permissions |} }
    /// Sets the Certificate permissions of the permission set.
    [<CustomOperation "certificate_permissions">]
    member __.SetCertificatePermissions(state:AccessPolicy, permissions) = { state with Permissions = {| state.Permissions with Certificates = Set permissions |} }

[<RequireQualifiedAccess>]
type SimpleCreateMode = Recover | Default
type KeyVaultBuilderState =
    { Name : ResourceName
      Access : KeyVaultSettings
      Sku : KeyVaultSku
      TenantId : Guid
      CreateMode : SimpleCreateMode option
      Policies : AccessPolicy list
      Uri : Uri option }    
let private zero =
    { Name = ResourceName.Empty
      TenantId = Guid.Empty
      Access = { VirtualMachineAccess = None; ResourceManagerAccess = None; AzureDiskEncryptionAccess = None; SoftDelete = None }
      Sku = KeyVaultSku.Standard
      Policies = []
      CreateMode = None
      Uri = None }
type KeyVaultBuilder() =
    member __.Yield (_:unit) = zero
    member __.Run(state:KeyVaultBuilderState) =
        { Name = state.Name
          Access = state.Access
          Sku = state.Sku
          TenantId = state.TenantId
          Policies =
            match state.CreateMode, state.Policies with
            | None, policies -> Unspecified policies
            | Some SimpleCreateMode.Default, policies -> Default policies
            | Some SimpleCreateMode.Recover, primary :: secondary -> Recover(primary, secondary)
            | Some SimpleCreateMode.Recover, [] -> failwith "Setting the creation mode to Recover requires at least one access policy. Use the accessPolicy builder to create a policy, and add it to the vault configuration using add_access_policy."
          Uri = state.Uri }
    /// Sets the name of the vault.
    [<CustomOperation "name">]
    member __.Name(state:KeyVaultBuilderState, name) = { state with Name = name }
    member this.Name(state:KeyVaultBuilderState, name) = this.Name(state, ResourceName name)
    /// Sets the sku of the vault.
    [<CustomOperation "sku">]
    member __.Sku(state:KeyVaultBuilderState, sku) = { state with Sku = sku }
    /// Sets the Tenant ID of the vault.
    [<CustomOperation "tenant_id">]
    member __.SetTenantId(state:KeyVaultBuilderState, tenantId) = { state with TenantId = tenantId }
    /// Allows VM access to the vault.
    [<CustomOperation "enable_vm_access">]
    member __.EnableVmAccess(state:KeyVaultBuilderState) = { state with Access = { state.Access with VirtualMachineAccess = Some Enabled } }
    /// Disallows VM access to the vault.
    [<CustomOperation "disable_vm_access">]
    member __.DisableVmAccess(state:KeyVaultBuilderState) = { state with Access = { state.Access with VirtualMachineAccess = Some Disabled } }
    /// Allows Resource Manager access to the vault.
    [<CustomOperation "enable_resource_manager_access">]
    member __.EnableResourceManagerAccess(state:KeyVaultBuilderState) = { state with Access = { state.Access with ResourceManagerAccess = Some Enabled } }
    /// Disallows Resource Manager access to the vault.
    [<CustomOperation "disable_resource_manager_access">]
    member __.DisableResourceManagerAccess(state:KeyVaultBuilderState) = { state with Access = { state.Access with ResourceManagerAccess = Some Disabled } }
    /// Allows Azure Disk Encyption service access to the vault.
    [<CustomOperation "enable_disk_encryption_access">]
    member __.EnableDiskEncryptionAccess(state:KeyVaultBuilderState) = { state with Access = { state.Access with AzureDiskEncryptionAccess = Some Enabled } }
    /// Disallows Azure Disk Encyption service access to the vault.
    [<CustomOperation "disable_disk_encryption_access">]
    member __.DisableDiskEncryptionAccess(state:KeyVaultBuilderState) = { state with Access = { state.Access with AzureDiskEncryptionAccess = Some Disabled } }
    /// Enables VM access to the vault.
    [<CustomOperation "enable_soft_delete">]
    member __.EnableSoftDeletion(state:KeyVaultBuilderState) = { state with Access = { state.Access with SoftDelete = Some SoftDeletionOnly } }
    /// Disables VM access to the vault.
    [<CustomOperation "enable_soft_delete_with_purge_protection">]
    member __.EnableSoftDeletionWithPurgeProtection(state:KeyVaultBuilderState) = { state with Access = { state.Access with SoftDelete = Some SoftDeleteWithPurgeProtection } }
    /// Sets the URI of the vault.
    [<CustomOperation "uri">]
    member __.Uri(state:KeyVaultBuilderState, uri) = { state with Uri = uri }
    /// Sets the Creation Mode to Recovery.
    [<CustomOperation "enable_recovery_mode">]
    member __.EnableRecoveryMode(state:KeyVaultBuilderState) = { state with CreateMode = Some SimpleCreateMode.Recover }
    /// Sets the Creation Mode to Default.
    [<CustomOperation "disable_recovery_mode">]
    member __.DisableRecoveryMode(state:KeyVaultBuilderState) = { state with CreateMode = Some SimpleCreateMode.Default }
    /// Adds an access policy to the vault.
    [<CustomOperation "add_access_policy">]
    member __.AddAccessPolicy(state:KeyVaultBuilderState, accessPolicy) = { state with Policies = accessPolicy :: state.Policies }

module Converters =
    let inline toStringArray theSet = theSet |> Set.map(fun s -> s.ToString().ToLower()) |> Set.toArray
    let inline maybeBoolean (f:FeatureFlag) = f.AsBoolean
    let keyVault location (kvc:KeyVaultConfig) : Models.KeyVault =
        { Name = kvc.Name
          Location = location          
          TenantId = kvc.TenantId.ToString()
          Sku = kvc.Sku.ToString().ToLower()

          EnabledForTemplateDeployment = kvc.Access.ResourceManagerAccess |> Option.map maybeBoolean
          EnabledForDiskEncryption = kvc.Access.AzureDiskEncryptionAccess |> Option.map maybeBoolean
          EnabledForDeployment = kvc.Access.VirtualMachineAccess |> Option.map maybeBoolean
          EnableSoftDelete =
            match kvc.Access.SoftDelete with
            | None ->
                None
            | Some SoftDeleteWithPurgeProtection
            | Some SoftDeletionOnly ->
                Some true          
          EnablePurgeProtection =
            match kvc.Access.SoftDelete with
            | None
            | Some SoftDeletionOnly ->
                None
            | Some SoftDeleteWithPurgeProtection ->
                Some true
          CreateMode =
            match kvc.Policies with
            | Unspecified _ -> None
            | Recover _ -> Some "recover"
            | Default _ -> Some "default"
          AccessPolicies =
            let policies =
                match kvc.Policies with                
                | Unspecified policies -> policies
                | Recover(policy, secondaryPolicies) -> policy :: secondaryPolicies
                | Default policies -> policies
            [| for policy in policies do
                {| ObjectId = policy.ObjectId
                   ApplicationId = policy.ApplicationId |> Option.map string
                   Permissions =
                    {| Certificates = policy.Permissions.Certificates |> toStringArray
                       Storage = policy.Permissions.Storage |> toStringArray
                       Keys = policy.Permissions.Keys |> toStringArray
                       Secrets = policy.Permissions.Secrets |> toStringArray |}
                |}
            |]
          Uri = kvc.Uri |> Option.map string
          DefaultAction = "AzureServices"
          Bypass = None }

let accessPolicy = AccessPolicyBuilder()
let keyVault = KeyVaultBuilder()