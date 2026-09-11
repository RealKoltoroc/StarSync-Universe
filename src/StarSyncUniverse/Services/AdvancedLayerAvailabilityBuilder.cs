using StarSyncUniverse.Data;
using StarSyncUniverse.Domain;

namespace StarSyncUniverse.Services;

public static class AdvancedLayerAvailabilityBuilder
{
    public static IReadOnlyList<AdvancedLayerAvailabilityRecord> Build(UniverseDataset dataset, ScUnpackedCatalog? catalog)
    {
        var sameBuild = catalog?.IsBuildCompatible(dataset.Build) == true;
        var candidate = catalog?.SourcePath;
        var p4 = catalog?.SourceP4;

        return
        [
            new AdvancedLayerAvailabilityRecord(
                "RESOURCES_MINING_SALVAGE",
                dataset.Build,
                candidate is null ? null : Path.Combine(Path.GetDirectoryName(candidate) ?? string.Empty, "resources", "locations.json"),
                p4,
                sameBuild,
                AuthoritativeImportEnabled: false,
                "CURRENT Data.p4k required for authority; SCUnpacked may only enrich when same-build verified",
                sameBuild ? "DISCOVERY_SOURCE_PRESENT_IMPORT_NOT_YET_PROVEN" : "BLOCKED_BY_BUILD_MISMATCH",
                sameBuild
                    ? "Same-build catalog is present, but mapping to current direct placement/spawn semantics is not yet proven."
                    : "Available SCUnpacked resource catalog is from a different P4 and must remain reference/discovery only."),
            new AdvancedLayerAvailabilityRecord(
                "ENVIRONMENT_GRAVITY_ATMOSPHERE_TEMPERATURE",
                dataset.Build,
                null,
                null,
                false,
                AuthoritativeImportEnabled: false,
                "CURRENT Data.p4k/DataCore only",
                "LOCAL_SOURCE_DISCOVERY_PENDING",
                "No authoritative per-body environment field mapping has been proven by the current root-ObjectContainer scan."),
            new AdvancedLayerAvailabilityRecord(
                "DYNAMIC_ORBITS",
                dataset.Build,
                "DataCore EntityClassDefinition.OrbitingObjectContainer/SOrbitComponentParams",
                null,
                true,
                AuthoritativeImportEnabled: false,
                "CURRENT DataCore capability definition",
                "CAPABILITY_LOCAL_DIRECT_INSTANCE_PARAMETERS_UNPROVEN",
                "Orbit capability is direct, but current non-zero per-instance radius/speed/angle parameters have not been proven."),
        ];
    }
}
