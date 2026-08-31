window.smartFieldGeolocation = {
    getCurrentPosition: () => new Promise((resolve) => {
        if (!navigator.geolocation) {
            resolve({
                status: 'unsupported',
                latitude: null,
                longitude: null,
                accuracyMeters: null,
                errorMessage: 'Este browser não suporta geolocalização.'
            });
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (position) => {
                resolve({
                    status: 'success',
                    latitude: position.coords.latitude,
                    longitude: position.coords.longitude,
                    accuracyMeters: position.coords.accuracy,
                    errorMessage: null
                });
            },
            (error) => {
                const statusByCode = {
                    1: 'permission-denied',
                    2: 'position-unavailable',
                    3: 'timeout'
                };

                resolve({
                    status: statusByCode[error.code] ?? 'unknown-error',
                    latitude: null,
                    longitude: null,
                    accuracyMeters: null,
                    errorMessage: error.message || 'Não foi possível obter a localização.'
                });
            },
            {
                enableHighAccuracy: true,
                timeout: 15000,
                maximumAge: 0
            });
    })
};
