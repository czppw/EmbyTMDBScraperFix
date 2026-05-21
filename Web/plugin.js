(function () {
    var pluginId = 'b6b08b4b-1b7d-4f1a-9d6c-f1f6b1ef0a11';
    var pluginVersion = '1.0.0';

    window.Dashboard = window.Dashboard || {};

    window.Dashboard.getPluginPages = function () {
        return [
            {
                name: 'EmbyTMDBScraperFix',
                path: Dashboard.getConfigurationPageUrl('EmbyTMDBScraperFix'),
                plugin: 'EmbyTMDBScraperFix',
                icon: 'tv'
            }
        ];
    };

    window.Dashboard.getPluginRoutes = function () {
        return [
            {
                path: '/plugins/embytmdbscraperfix.html',
                id: 'embytmdbscraperfix',
                controller: 'plugins/embytmdbscraperfix/embytmdbscraperfix.js?v=' + pluginVersion,
                template: 'plugins/embytmdbscraperfix/embytmdbscraperfix.html?v=' + pluginVersion,
                title: 'EmbyTMDBScraperFix',
                pluginId: pluginId,
                mobile: true
            }
        ];
    };
})();
