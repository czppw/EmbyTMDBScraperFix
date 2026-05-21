(function () {
    const pluginVersion = '1.0.0';

    window.Dashboard.getPluginPages = function () {
        return [
            {
                name: 'EmbyTMDBScraperFix',
                path: Dashboard.getConfigurationPageUrl('EmbyTMDBScraperFixConfiguration'),
                plugin: 'EmbyTMDBScraperFix',
                icon: 'tv'
            }
        ];
    };

    window.Dashboard.getPluginRoutes = function () {
        return [
            {
                path: '/plugins/embytmdbscraperfixconfiguration.html',
                id: 'embytmdbscraperfixconfiguration',
                controller: 'plugins/embytmdbscraperfix/embytmdbscraperfixconfiguration.js?v=' + pluginVersion,
                template: 'plugins/embytmdbscraperfix/embytmdbscraperfixconfiguration.html?v=' + pluginVersion,
                title: 'EmbyTMDBScraperFix',
                mobile: true
            }
        ];
    };
})();
