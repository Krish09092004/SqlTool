class ApiClient {
    constructor() {
        this.baseUrl = '/Home'; // Or get from CONSTANTS if available
    }

    async apiCall(method, url, data = null) {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';
        
        const options = {
            method: method,
            headers: {
                'Content-Type': 'application/json',
                'X-Requested-With': 'XMLHttpRequest',
                'RequestVerificationToken': token
            }
        };

        if (data) {
            options.body = JSON.stringify(data);
        }

        try {
            const response = await fetch(url, options);

            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }

            return await response.json();
        } catch (error) {
            console.error('API call failed:', error);
            throw error;
        }
    }

    async getDatabases(serverName) {
        return await this.apiCall('GET', `${CONSTANTS.API.GET_DATABASES}?serverName=${encodeURIComponent(serverName)}`);
    }

    async getTableColumns(server, database, tableName) {
        return await this.apiCall('GET',
            `${CONSTANTS.API.GET_TABLE_COLUMNS}?serverName=${encodeURIComponent(server)}&databaseName=${encodeURIComponent(database)}&tableName=${encodeURIComponent(tableName)}`
        );
    }

    async generateTemplate(requestData) {
        return await this.apiCall('POST', '/Home/GenerateTemplate', requestData);
    }

    async downloadTemplate(requestData) {
        // Download logic is slightly different as it returns a blob/file, 
        // but typically we might just trigger a form post or use fetch to get blob.
        // For consistency with existing logic which uses fetch and blob:
        
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        const token = tokenInput ? tokenInput.value : '';

        const response = await fetch('/Home/DownloadTemplate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify(requestData)
        });

        if (response.ok) {
            return await response.blob();
        } else {
            throw new Error('Download failed');
        }
    }
}
