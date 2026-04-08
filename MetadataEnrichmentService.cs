using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace LocalGameDownloader
{
    public class MetadataEnrichmentService
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public void TryApplyMetadata(IPlayniteAPI api, GameMetadata metadata)
        {
            if (api == null || metadata == null || string.IsNullOrWhiteSpace(metadata.Name))
            {
                return;
            }

            try
            {
                var igdbPluginId = BuiltinExtensions.GetIdFromExtension(BuiltinExtension.IgdbMetadata);
                var metadataPlugin = api.Addons.Plugins
                    .OfType<MetadataPlugin>()
                    .FirstOrDefault(plugin => plugin.Id == igdbPluginId);

                if (metadataPlugin == null)
                {
                    logger.Warn("IGDB metadata plugin is not available.");
                    return;
                }

                var lookupGame = CreateLookupGame(metadata);
                var args = new GetMetadataFieldArgs();

                try
                {
                    using (var provider = metadataPlugin.GetMetadataProvider(new MetadataRequestOptions(lookupGame, false)))
                    {
                        if (provider == null)
                        {
                            logger.Warn($"Metadata provider '{metadataPlugin.Name}' returned no provider for '{metadata.Name}'.");
                            return;
                        }

                        var availableFields = GetSupportedFields(metadataPlugin, provider);
                        ApplyProviderMetadata(metadata, provider, availableFields, args);
                    }
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, $"Metadata provider '{metadataPlugin.Name}' failed for '{metadata.Name}'.");
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Metadata enrichment failed for '{metadata?.Name}'.");
            }
        }

        private static Game CreateLookupGame(GameMetadata metadata)
        {
            var lookupGame = new Game
            {
                Name = metadata.Name,
                InstallDirectory = metadata.InstallDirectory,
                ReleaseDate = metadata.ReleaseDate,
                Description = metadata.Description,
                GameId = metadata.GameId
            };

            if (metadata.Links != null && metadata.Links.Count > 0)
            {
                lookupGame.Links = new ObservableCollection<Link>(metadata.Links);
            }

            return lookupGame;
        }

        private static void ApplyProviderMetadata(
            GameMetadata metadata,
            OnDemandMetadataProvider provider,
            ISet<MetadataField> availableFields,
            GetMetadataFieldArgs args)
        {
            if (metadata == null || provider == null)
            {
                return;
            }

            if (availableFields.Contains(MetadataField.Description) && string.IsNullOrWhiteSpace(metadata.Description))
            {
                metadata.Description = SafeGetValue(() => provider.GetDescription(args)) ?? metadata.Description;
            }

            if (availableFields.Contains(MetadataField.ReleaseDate) && !metadata.ReleaseDate.HasValue)
            {
                metadata.ReleaseDate = SafeGetValue(() => provider.GetReleaseDate(args)) ?? metadata.ReleaseDate;
            }

            if (availableFields.Contains(MetadataField.CriticScore) && !metadata.CriticScore.HasValue)
            {
                metadata.CriticScore = SafeGetValue(() => provider.GetCriticScore(args)) ?? metadata.CriticScore;
            }

            if (availableFields.Contains(MetadataField.CommunityScore) && !metadata.CommunityScore.HasValue)
            {
                metadata.CommunityScore = SafeGetValue(() => provider.GetCommunityScore(args)) ?? metadata.CommunityScore;
            }

            if (availableFields.Contains(MetadataField.InstallSize) && !metadata.InstallSize.HasValue)
            {
                metadata.InstallSize = SafeGetValue(() => provider.GetInstallSize(args)) ?? metadata.InstallSize;
            }

            metadata.Genres = MergeMetadataProperties(
                metadata.Genres,
                SafeGetMetadataPropertyList(() => provider.GetGenres(args)));

            metadata.Developers = MergeMetadataProperties(
                metadata.Developers,
                SafeGetMetadataPropertyList(() => provider.GetDevelopers(args)));

            metadata.Publishers = MergeMetadataProperties(
                metadata.Publishers,
                SafeGetMetadataPropertyList(() => provider.GetPublishers(args)));

            metadata.Tags = MergeMetadataProperties(
                metadata.Tags,
                SafeGetMetadataPropertyList(() => provider.GetTags(args)));

            metadata.Features = MergeMetadataProperties(
                metadata.Features,
                SafeGetMetadataPropertyList(() => provider.GetFeatures(args)));

            metadata.AgeRatings = MergeMetadataProperties(
                metadata.AgeRatings,
                SafeGetMetadataPropertyList(() => provider.GetAgeRatings(args)));

            metadata.Series = MergeMetadataProperties(
                metadata.Series,
                SafeGetMetadataPropertyList(() => provider.GetSeries(args)));

            metadata.Regions = MergeMetadataProperties(
                metadata.Regions,
                SafeGetMetadataPropertyList(() => provider.GetRegions(args)));

            metadata.Platforms = MergeMetadataProperties(
                metadata.Platforms,
                SafeGetMetadataPropertyList(() => provider.GetPlatforms(args)));

            if (availableFields.Contains(MetadataField.Links))
            {
                metadata.Links = MergeLinks(metadata.Links, SafeGetValue(() => provider.GetLinks(args)));
            }

            if (availableFields.Contains(MetadataField.Icon) && metadata.Icon == null)
            {
                metadata.Icon = SafeGetValue(() => provider.GetIcon(args)) ?? metadata.Icon;
            }

            if (availableFields.Contains(MetadataField.CoverImage) && metadata.CoverImage == null)
            {
                metadata.CoverImage = SafeGetValue(() => provider.GetCoverImage(args)) ?? metadata.CoverImage;
            }

            if (availableFields.Contains(MetadataField.BackgroundImage) && metadata.BackgroundImage == null)
            {
                metadata.BackgroundImage = SafeGetValue(() => provider.GetBackgroundImage(args)) ?? metadata.BackgroundImage;
            }
        }

        private static ISet<MetadataField> GetSupportedFields(MetadataPlugin metadataPlugin, OnDemandMetadataProvider provider)
        {
            var fields = provider?.AvailableFields?.ToHashSet() ?? new HashSet<MetadataField>();
            if (fields.Count == 0 && metadataPlugin?.SupportedFields != null)
            {
                fields = metadataPlugin.SupportedFields.ToHashSet();
            }

            return fields;
        }

        private static IEnumerable<MetadataProperty> SafeGetMetadataPropertyList(Func<IEnumerable<MetadataProperty>> getter)
        {
            if (getter == null)
            {
                return null;
            }

            try
            {
                return getter()?
                    .Where(item => item != null && !string.IsNullOrWhiteSpace(GetMetadataPropertyKey(item)))
                    .ToList();
            }
            catch
            {
                return null;
            }
        }

        private static T SafeGetValue<T>(Func<T> getter)
        {
            if (getter == null)
            {
                return default(T);
            }

            try
            {
                return getter();
            }
            catch
            {
                return default(T);
            }
        }

        private static HashSet<MetadataProperty> MergeMetadataProperties(
            HashSet<MetadataProperty> target,
            IEnumerable<MetadataProperty> source)
        {
            var merged = target != null
                ? new HashSet<MetadataProperty>(target.Where(item => item != null && !string.IsNullOrWhiteSpace(GetMetadataPropertyKey(item))))
                : new HashSet<MetadataProperty>();

            var values = source?
                .Where(item => item != null && !string.IsNullOrWhiteSpace(GetMetadataPropertyKey(item)))
                .ToList();

            if (values == null || values.Count == 0)
            {
                return merged.Count > 0 ? merged : null;
            }

            var existingKeys = new HashSet<string>(
                merged.Select(GetMetadataPropertyKey),
                StringComparer.OrdinalIgnoreCase);

            foreach (var item in values)
            {
                if (existingKeys.Add(GetMetadataPropertyKey(item)))
                {
                    merged.Add(item);
                }
            }

            return merged;
        }

        private static List<Link> MergeLinks(List<Link> target, IEnumerable<Link> links)
        {
            var merged = target?.Where(link => link != null).ToList() ?? new List<Link>();
            if (links == null)
            {
                return merged.Count > 0 ? merged : null;
            }

            var existingKeys = new HashSet<string>(
                merged.Select(BuildLinkKey),
                StringComparer.OrdinalIgnoreCase);

            foreach (var link in links.Where(link => link != null))
            {
                var key = BuildLinkKey(link);
                if (existingKeys.Add(key))
                {
                    merged.Add(link);
                }
            }

            return merged.Count > 0 ? merged : null;
        }

        private static string BuildLinkKey(Link link)
        {
            if (link == null)
            {
                return string.Empty;
            }

            return $"{link.Name}|{link.Url}";
        }

        private static string GetMetadataPropertyKey(MetadataProperty property)
        {
            if (property == null)
            {
                return string.Empty;
            }

            if (property is MetadataNameProperty nameProperty)
            {
                return nameProperty.Name ?? string.Empty;
            }

            return property.ToString() ?? string.Empty;
        }
    }
}
