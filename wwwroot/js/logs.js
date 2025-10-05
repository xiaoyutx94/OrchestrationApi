// 自定义确认框函数
function showConfirm(message, title = '确认操作') {
    return new Promise((resolve) => {
        const modal = document.getElementById('confirmModal');
        const messageEl = document.getElementById('confirmMessage');
        const titleEl = modal.querySelector('h3');
        const yesBtn = document.getElementById('confirmYes');
        const noBtn = document.getElementById('confirmNo');

        messageEl.textContent = message;
        titleEl.textContent = title;
        modal.classList.remove('hidden');

        // 添加淡入动画
        setTimeout(() => {
            modal.querySelector('.relative').style.transform = 'scale(1)';
            modal.querySelector('.relative').style.opacity = '1';
        }, 10);

        const handleYes = () => {
            modal.classList.add('hidden');
            yesBtn.removeEventListener('click', handleYes);
            noBtn.removeEventListener('click', handleNo);
            document.removeEventListener('keydown', handleKeydown);
            resolve(true);
        };

        const handleNo = () => {
            modal.classList.add('hidden');
            yesBtn.removeEventListener('click', handleYes);
            noBtn.removeEventListener('click', handleNo);
            document.removeEventListener('keydown', handleKeydown);
            resolve(false);
        };

        const handleKeydown = (e) => {
            if (e.key === 'Enter') {
                handleYes();
            } else if (e.key === 'Escape') {
                handleNo();
            }
        };

        yesBtn.addEventListener('click', handleYes);
        noBtn.addEventListener('click', handleNo);
        document.addEventListener('keydown', handleKeydown);

        // 点击遮罩关闭
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                handleNo();
            }
        });
    });
}

// 自定义提示框函数
function showAlert(message, type = 'info', title = '提示') {
    return new Promise((resolve) => {
        const modal = document.getElementById('alertModal');
        const messageEl = document.getElementById('alertMessage');
        const titleEl = document.getElementById('alertTitle');
        const iconEl = document.getElementById('alertIcon');
        const okBtn = document.getElementById('alertOk');

        messageEl.textContent = message;
        titleEl.textContent = title;

        // 根据类型设置图标和颜色
        let iconHTML = '';
        let buttonClass = 'bg-blue-500 hover:bg-blue-600 focus:ring-blue-300';

        switch (type) {
            case 'success':
                iconHTML = `
                            <div class="bg-green-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-green-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M5 13l4 4L19 7"></path>
                                </svg>
                            </div>`;
                buttonClass = 'bg-green-500 hover:bg-green-600 focus:ring-green-300';
                break;
            case 'error':
                iconHTML = `
                            <div class="bg-red-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M6 18L18 6M6 6l12 12"></path>
                                </svg>
                            </div>`;
                buttonClass = 'bg-red-500 hover:bg-red-600 focus:ring-red-300';
                break;
            case 'warning':
                iconHTML = `
                            <div class="bg-yellow-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-yellow-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16c-.77.833.192 2.5 1.732 2.5z"></path>
                                </svg>
                            </div>`;
                buttonClass = 'bg-yellow-500 hover:bg-yellow-600 focus:ring-yellow-300';
                break;
            default: // info
                iconHTML = `
                            <div class="bg-blue-100 rounded-full p-3">
                                <svg class="h-6 w-6 text-blue-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z"></path>
                                </svg>
                            </div>`;
        }

        iconEl.innerHTML = iconHTML;
        okBtn.className = `px-4 py-2 text-white text-base font-medium rounded-md w-20 focus:outline-none transition-colors ${buttonClass}`;

        modal.classList.remove('hidden');

        // 添加淡入动画
        setTimeout(() => {
            modal.querySelector('.relative').style.transform = 'scale(1)';
            modal.querySelector('.relative').style.opacity = '1';
        }, 10);

        const handleOk = () => {
            modal.classList.add('hidden');
            okBtn.removeEventListener('click', handleOk);
            document.removeEventListener('keydown', handleKeydown);
            resolve();
        };

        const handleKeydown = (e) => {
            if (e.key === 'Enter' || e.key === 'Escape') {
                handleOk();
            }
        };

        okBtn.addEventListener('click', handleOk);
        document.addEventListener('keydown', handleKeydown);

        // 点击遮罩关闭
        modal.addEventListener('click', (e) => {
            if (e.target === modal) {
                handleOk();
            }
        });
    });
}


function logsManagement() {
    return {
        logs: [],
        logDetail: null,
        showDetailModal: false,
        selectedLogs: [],
        currentPage: 1,
        pageSize: 20,
        totalCount: 0,
        totalRequests: 0,
        successRequests: 0,
        errorRequests: 0,
        avgResponseTime: 0,
        totalTokensUsed: 0,
        successTokensUsed: 0,
        avgTokensPerRequest: 0,
        tokenSuccessRate: 0,
        proxyKeys: [],
        providerGroups: [],
        models: [],
        filters: {
            proxyKeyName: '',
            providerGroup: '',
            model: '',
            status: '',
            stream: ''
        },

        // 新增功能
        autoRefresh: false,
        refreshInterval: null,
        nextUpdateCountdown: 30,
        countdownInterval: null,
        chartTimeRange: '',
        chartsLoading: {
            status: false,
            model: false,
            tokenTrend: false,
            groupToken: false
        },
        fullscreenChart: false,
        fullscreenChartTitle: '',
        fullscreenChartContent: '',
        previousStats: {
            totalTokensUsed: 0,
            successTokensUsed: 0,
            avgTokensPerRequest: 0,
            tokenSuccessRate: 0
        },

        async init() {
            await this.loadFilterOptions(); // 优先加载筛选选项
            await this.loadLogs();
            await this.loadStats();
            await this.loadTokenStats();
            // 延迟初始化图表，确保数据已加载和DOM已渲染
            setTimeout(() => {
                this.initCharts();
            }, 500);
        },

        get totalPages() {
            return Math.ceil(this.totalCount / this.pageSize);
        },

        async loadLogs() {
            try {
                const params = new URLSearchParams({
                    limit: this.pageSize,
                    offset: (this.currentPage - 1) * this.pageSize
                });

                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.status) params.append('status', this.filters.status);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs?${params}`);
                const data = await response.json();

                if (data.success) {
                    this.logs = data.logs || [];
                    this.totalCount = data.total_count || 0;
                    this.selectedLogs = []; // 清空选中状态
                    // 不再从当前页数据中提取筛选项
                    // this.extractFilters(); // 已移除
                } else {
                    console.error('Failed to load logs:', data.error);
                }
            } catch (error) {
                console.error('Error loading logs:', error);
            }
        },

        async loadFilterOptions() {
            try {
                const response = await fetch('/admin/logs/filter-options');
                const data = await response.json();

                if (data.success) {
                    this.proxyKeys = data.filter_options?.proxy_keys || [];
                    this.providerGroups = data.filter_options?.provider_groups || [];
                    this.models = data.filter_options?.models || [];
                } else {
                    console.error('Failed to load filter options:', data.error);
                }
            } catch (error) {
                console.error('Error loading filter options:', error);
            }
        },

        async loadStats() {
            try {
                const [proxyKeyStatsResponse, modelStatsResponse] = await Promise.all([
                    fetch('/admin/logs/stats/api-keys'),
                    fetch('/admin/logs/stats/models')
                ]);

                const proxyKeyStats = await proxyKeyStatsResponse.json();
                const modelStats = await modelStatsResponse.json();

                if (proxyKeyStats.success) {
                    const stats = proxyKeyStats.stats || [];
                    this.totalRequests = stats.reduce((sum, stat) => sum + stat.total_requests, 0);
                    this.successRequests = stats.reduce((sum, stat) => sum + stat.success_requests, 0);
                    this.errorRequests = stats.reduce((sum, stat) => sum + stat.error_requests, 0);
                    this.avgResponseTime = Math.round(stats.reduce((sum, stat) => sum + stat.avg_duration, 0) / stats.length) || 0;
                }
            } catch (error) {
                console.error('Error loading stats:', error);
            }
        },

        // 已弃用：此方法仅从当前页提取筛选项，现在使用独立的API端点加载全局筛选选项
        // extractFilters() {
        //     const proxyKeys = new Set();
        //     const providerGroups = new Set();
        //     const models = new Set();
        //
        //     this.logs.forEach(log => {
        //         if (log.proxy_key_name) {
        //             proxyKeys.add(log.proxy_key_name);
        //         }
        //         if (log.provider_group) {
        //             providerGroups.add(log.provider_group);
        //         }
        //         if (log.model) {
        //             models.add(log.model);
        //         }
        //     });
        //
        //     this.proxyKeys = Array.from(proxyKeys).sort();
        //     this.providerGroups = Array.from(providerGroups).sort();
        //     this.models = Array.from(models).sort();
        // },

        async applyFilters() {
            this.currentPage = 1;
            await this.loadLogs();
            await this.loadStats();
            // 筛选条件变化时也需要更新图表
            setTimeout(async () => {
                await this.updateCharts();
            }, 300);
        },

        async viewLogDetail(id) {
            try {
                const response = await fetch(`/admin/logs/${id}`);
                const data = await response.json();

                if (data.success) {
                    this.logDetail = data.log;
                    this.showDetailModal = true;
                } else {
                    console.error('Failed to load log detail:', data.error);
                }
            } catch (error) {
                console.error('Error loading log detail:', error);
            }
        },

        async refreshLogs() {
            await this.loadLogs();
            await this.loadStats();
            //await this.loadTokenStats();
            // 延迟更新图表，确保数据已加载
            setTimeout(() => {
                this.updateCharts();
            }, 300);
        },

        async loadTokenStats() {
            try {
                // 保存之前的统计数据用于趋势计算
                this.previousStats = {
                    totalTokensUsed: this.totalTokensUsed,
                    successTokensUsed: this.successTokensUsed,
                    avgTokensPerRequest: this.avgTokensPerRequest,
                    tokenSuccessRate: this.tokenSuccessRate
                };

                const response = await fetch('/admin/logs/stats/tokens');
                const data = await response.json();

                if (data.success && data.stats) {
                    this.totalTokensUsed = data.stats.total_tokens || 0;
                    this.successTokensUsed = data.stats.success_tokens || 0;
                    this.avgTokensPerRequest = data.stats.success_requests > 0 ?
                        Math.round(this.successTokensUsed / data.stats.success_requests) : 0;
                    this.tokenSuccessRate = data.stats.total_requests > 0 ?
                        Math.round((data.stats.success_requests / data.stats.total_requests) * 100) : 0;
                }
            } catch (error) {
                console.error('Error loading token stats:', error);
            }
        },

        refreshTokenStats() {
            this.loadTokenStats();
        },

        // 获取Token趋势
        getTokenTrend(type) {
            const current = this[type === 'total' ? 'totalTokensUsed' :
                type === 'success' ? 'successTokensUsed' :
                    type === 'avg' ? 'avgTokensPerRequest' : 'tokenSuccessRate'];
            const previous = this.previousStats[type === 'total' ? 'totalTokensUsed' :
                type === 'success' ? 'successTokensUsed' :
                    type === 'avg' ? 'avgTokensPerRequest' : 'tokenSuccessRate'];

            if (previous === 0) return '';

            const change = current - previous;
            const percentage = Math.abs(Math.round((change / previous) * 100));

            if (change > 0) {
                return `↗ +${percentage}%`;
            } else if (change < 0) {
                return `↘ -${percentage}%`;
            } else {
                return '→ 0%';
            }
        },

        // 实时更新功能
        toggleAutoRefresh() {
            this.autoRefresh = !this.autoRefresh;

            if (this.autoRefresh) {
                this.startAutoRefresh();
            } else {
                this.stopAutoRefresh();
            }
        },

        startAutoRefresh() {
            this.nextUpdateCountdown = 30;

            // 开始倒计时
            this.countdownInterval = setInterval(() => {
                this.nextUpdateCountdown--;
                if (this.nextUpdateCountdown <= 0) {
                    this.nextUpdateCountdown = 30;
                }
            }, 1000);

            // 开始自动刷新
            this.refreshInterval = setInterval(async () => {
                await this.refreshLogs();
            }, 30000);
        },

        stopAutoRefresh() {
            if (this.refreshInterval) {
                clearInterval(this.refreshInterval);
                this.refreshInterval = null;
            }
            if (this.countdownInterval) {
                clearInterval(this.countdownInterval);
                this.countdownInterval = null;
            }
        },

        previousPage() {
            if (this.currentPage > 1) {
                this.currentPage--;
                this.loadLogs();
            }
        },

        nextPage() {
            if (this.currentPage < this.totalPages) {
                this.currentPage++;
                this.loadLogs();
            }
        },

        formatDate(dateString) {
            if (!dateString) return '';
            const date = new Date(dateString);
            return date.toLocaleString('zh-CN');
        },

        formatJSON(jsonString) {
            if (!jsonString) return '';
            try {
                const obj = JSON.parse(jsonString);
                return JSON.stringify(obj, null, 2);
            } catch (e) {
                return jsonString;
            }
        },

        formatResponse(responseString) {
            if (!responseString) return '无响应内容';

            // 如果是流式响应，显示前几行
            if (responseString.includes('data: ')) {
                const lines = responseString.split('\n').slice(0, 10);
                return lines.join('\n') + (responseString.split('\n').length > 10 ? '\n...(更多内容)' : '');
            }

            // 尝试格式化JSON
            try {
                const obj = JSON.parse(responseString);
                return JSON.stringify(obj, null, 2);
            } catch (e) {
                return responseString;
            }
        },

        // 批量选择相关方法
        toggleLogSelection(logId) {
            const index = this.selectedLogs.indexOf(logId);
            if (index > -1) {
                this.selectedLogs.splice(index, 1);
            } else {
                this.selectedLogs.push(logId);
            }
        },

        toggleSelectAll() {
            if (this.isAllSelected()) {
                this.selectedLogs = [];
            } else {
                this.selectedLogs = this.logs.map(log => log.id);
            }
        },

        isAllSelected() {
            return this.logs.length > 0 && this.selectedLogs.length === this.logs.length;
        },

        // 删除选中的日志
        async deleteSelectedLogs() {
            if (this.selectedLogs.length === 0) {
                await showAlert('请先选择要删除的日志', 'warning', '提示');
                return;
            }

            const confirmed = await showConfirm(`确定要删除选中的 ${this.selectedLogs.length} 条日志吗？此操作不可撤销。`, '确认删除');
            if (confirmed) {
                await this.performDeleteSelected();
            }
        },

        async performDeleteSelected() {
            try {
                // 确保ID是数字类型
                const ids = this.selectedLogs.map(id => parseInt(id, 10)).filter(id => !isNaN(id));

                if (ids.length === 0) {
                    await showAlert('没有有效的日志ID可删除', 'warning', '提示');
                    return;
                }



                const response = await fetch('/admin/logs/batch', {
                    method: 'DELETE',
                    headers: {
                        'Content-Type': 'application/json',
                    },
                    body: JSON.stringify({
                        ids: ids
                    })
                });

                const data = await response.json();

                if (data.success) {
                    await showAlert(`成功删除 ${data.deleted_count} 条日志`, 'success', '提示');
                    this.selectedLogs = [];
                    await this.loadFilterOptions(); // 刷新筛选选项
                    this.loadLogs();
                    this.loadStats();
                } else {
                    await showAlert('删除失败: ' + data.error, 'error', '提示');
                }
            } catch (error) {
                console.error('Error deleting logs:', error);
                await showAlert('删除失败: ' + error.message, 'error', '提示');
            }
        },

        // 清理过期日志
        async cleanupExpiredLogs() {
            const confirmed = await showConfirm('确定要清理过期日志吗？此操作将删除超过保留期限的日志记录，不可撤销。', '确认清理过期日志');
            if (confirmed) {
                await this.performCleanupExpired();
            }
        },

        // 清空错误日志
        async clearErrorLogs() {
            const confirmed = await showConfirm('确定要清空所有错误日志吗？此操作将删除所有状态码不为200的日志记录，不可撤销。', '确认清空错误日志');
            if (confirmed) {
                await this.performClearErrors();
            }
        },

        // 清空所有日志
        async clearAllLogs() {
            const confirmed = await showConfirm('确定要清空所有日志吗？此操作将删除所有日志记录，不可撤销。', '确认清空');
            if (confirmed) {
                await this.performClearAll();
            }
        },

        async performCleanupExpired() {
            try {
                const response = await fetch('/admin/logs/cleanup', {
                    method: 'POST'
                });

                const data = await response.json();

                if (data.success) {
                    await showAlert(data.message || '过期日志清理完成', 'success', '提示');
                    this.selectedLogs = [];
                    await this.loadFilterOptions(); // 刷新筛选选项
                    this.loadLogs();
                    this.loadStats();
                } else {
                    await showAlert('清理过期日志失败: ' + data.error, 'error', '提示');
                }
            } catch (error) {
                console.error('Error cleaning up expired logs:', error);
                await showAlert('清理过期日志失败: ' + error.message, 'error', '提示');
            }
        },

        async performClearAll() {
            try {
                const response = await fetch('/admin/logs/clear', {
                    method: 'DELETE'
                });

                const data = await response.json();

                if (data.success) {
                    await showAlert(`成功清空所有日志，删除了 ${data.deleted_count} 条记录`, 'success', '提示');
                    this.selectedLogs = [];
                    await this.loadFilterOptions(); // 刷新筛选选项
                    this.loadLogs();
                    this.loadStats();
                } else {
                    await showAlert('清空失败: ' + data.error, 'error', '提示');
                }
            } catch (error) {
                console.error('Error clearing logs:', error);
                await showAlert('清空失败: ' + error.message, 'error', '提示');
            }
        },

        async performClearErrors() {
            try {
                const response = await fetch('/admin/logs/clear-errors', {
                    method: 'DELETE'
                });

                const data = await response.json();

                if (data.success) {
                    await showAlert(`成功清空错误日志，删除了 ${data.deleted_count} 条记录`, 'success', '提示');
                    this.selectedLogs = [];
                    await this.loadFilterOptions(); // 刷新筛选选项
                    this.loadLogs();
                    this.loadStats();
                } else {
                    await showAlert('清空错误日志失败: ' + data.error, 'error', '提示');
                }
            } catch (error) {
                console.error('Error clearing error logs:', error);
                await showAlert('清空错误日志失败: ' + error.message, 'error', '提示');
            }
        },

        // 导出日志
        async exportLogs() {
            try {
                const params = new URLSearchParams();
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.status) params.append('status', this.filters.status);
                if (this.filters.stream) params.append('type', this.filters.stream);
                params.append('format', 'csv');

                const url = `/admin/logs/export?${params}`;

                // 创建一个隐藏的链接来触发下载
                const link = document.createElement('a');
                link.href = url;
                link.style.display = 'none';
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
            } catch (error) {
                console.error('Error exporting logs:', error);
                await showAlert('导出失败: ' + error.message, 'error', '提示');
            }
        },

        // Token统计导出
        exportTokenStats() {
            const stats = [
                {
                    '指标': '总Token数',
                    '数值': this.totalTokensUsed,
                    '趋势': this.getTokenTrend('total')
                },
                {
                    '指标': '成功Token数',
                    '数值': this.successTokensUsed,
                    '趋势': this.getTokenTrend('success')
                },
                {
                    '指标': '平均Token/请求',
                    '数值': this.avgTokensPerRequest,
                    '趋势': this.getTokenTrend('avg')
                },
                {
                    '指标': 'Token成功率',
                    '数值': this.tokenSuccessRate + '%',
                    '趋势': this.getTokenTrend('rate')
                }
            ];

            this.exportToCSV(stats, 'token_stats');
        },

        // CSV导出工具
        exportToCSV(data, filename = 'data') {
            if (!data || data.length === 0) return;

            const headers = Object.keys(data[0]);
            const csvContent = [
                headers.join(','),
                ...data.map(row => headers.map(header => {
                    const value = row[header];
                    return typeof value === 'string' && value.includes(',') ? `"${value}"` : value;
                }).join(','))
            ].join('\n');

            const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
            const link = document.createElement('a');
            link.href = URL.createObjectURL(blob);
            link.download = `${filename}.csv`;
            link.click();
        },


        // 图表实例管理
        chartInstances: {
            status: null,
            model: null,
            tokenTrend: null,
            groupToken: null
        },

        // 图表相关方法
        initCharts() {
            // 确保DOM已渲染且数据已加载
            if (typeof Chart === 'undefined') {
                console.warn('Chart.js not loaded');
                return;
            }

            try {
                this.createStatusChart();
                this.createModelChart();
                this.createTokenTrendChart();
                this.createGroupTokenChart();
            } catch (error) {
                console.error('Error initializing charts:', error);
            }
        },

        updateCharts() {
            // 销毁现有图表实例
            this.destroyCharts();
            // 重新创建图表
            this.initCharts();
        },

        destroyCharts() {
            Object.keys(this.chartInstances).forEach(key => {
                if (this.chartInstances[key]) {
                    this.chartInstances[key].destroy();
                    this.chartInstances[key] = null;
                }
            });
        },

        updateChartsWithTimeRange() {
            // 根据时间范围更新图表数据
            this.updateCharts();
        },

        resetAllCharts() {
            // 重置所有图表缩放
            Object.values(this.chartInstances).forEach(chart => {
                if (chart && chart.resetZoom) {
                    chart.resetZoom();
                }
            });
        },

        exportChart(chartId) {
            const chart = this.chartInstances[chartId];
            if (chart) {
                const url = chart.toBase64Image();
                const link = document.createElement('a');
                link.download = `${chartId}_chart.png`;
                link.href = url;
                link.click();
            }
        },

        async exportAllCharts() {
            Object.keys(this.chartInstances).forEach(chartId => {
                this.exportChart(chartId);
            });
        },

        toggleFullscreen(chartId) {
            this.fullscreenChart = !this.fullscreenChart;
            this.fullscreenChartTitle = chartId;

            if (this.fullscreenChart) {
                // 延迟创建全屏图表，确保DOM已渲染
                setTimeout(async () => {
                    await this.createFullscreenChart(chartId);
                }, 100);
            }
        },

        exitFullscreen() {
            this.fullscreenChart = false;
            // 销毁全屏图表实例
            if (this.fullscreenChartInstance) {
                this.fullscreenChartInstance.destroy();
                this.fullscreenChartInstance = null;
            }
        },

        // 全屏图表实例
        fullscreenChartInstance: null,

        async createFullscreenChart(chartId) {
            const canvas = document.getElementById('fullscreenChart');
            if (!canvas) return;

            const ctx = canvas.getContext('2d');

            // 销毁之前的全屏图表实例
            if (this.fullscreenChartInstance) {
                this.fullscreenChartInstance.destroy();
            }

            // 根据图表类型创建对应的全屏图表
            switch (chartId) {
                case 'statusChart':
                    await this.createFullscreenStatusChart(ctx);
                    break;
                case 'modelChart':
                    await this.createFullscreenModelChart(ctx);
                    break;
                case 'tokenTrendChart':
                    await this.createFullscreenTokenTrendChart(ctx);
                    break;
                case 'groupTokenChart':
                    await this.createFullscreenGroupTokenChart(ctx);
                    break;
            }
        },

        async createFullscreenStatusChart(ctx) {
            // 使用全局聚合数据
            let successCount = 0;
            let errorCount = 0;

            try {
                const params = new URLSearchParams();
                if (this.chartTimeRange) params.append('range', this.chartTimeRange);
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs/stats/status?${params}`);
                const result = await response.json();

                if (result.success && result.data) {
                    successCount = result.data.success || 0;
                    errorCount = result.data.error || 0;
                }
            } catch (error) {
                console.error('Failed to fetch status stats for fullscreen:', error);
                successCount = this.successRequests || 0;
                errorCount = this.errorRequests || 0;
            }

            const labels = ['成功请求', '失败请求'];
            const data = [successCount, errorCount];
            const colors = ['#10B981', '#EF4444'];

            this.fullscreenChartInstance = new Chart(ctx, {
                type: 'doughnut',
                data: {
                    labels: labels,
                    datasets: [{
                        data: data,
                        backgroundColor: colors,
                        borderWidth: 2,
                        borderColor: '#ffffff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                padding: 20,
                                usePointStyle: true,
                                font: { size: 16 }
                            }
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                    const percentage = total > 0 ? ((context.parsed * 100) / total).toFixed(1) : 0;
                                    return `${context.label}: ${context.parsed} (${percentage}%)`;
                                }
                            }
                        }
                    }
                }
            });
        },

        async createFullscreenModelChart(ctx) {
            // 使用全局聚合数据
            let labels = [];
            let data = [];

            try {
                const params = new URLSearchParams();
                if (this.chartTimeRange) params.append('range', this.chartTimeRange);
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs/stats/models?${params}`);
                const result = await response.json();

                if (result.success && result.stats) {
                    const sortedModels = result.stats
                        .sort((a, b) => b.total_requests - a.total_requests)
                        .slice(0, 10);

                    labels = sortedModels.map(stat => stat.model || 'unknown');
                    data = sortedModels.map(stat => stat.total_requests || 0);
                }
            } catch (error) {
                console.error('Failed to fetch model stats for fullscreen:', error);
                // 降级到使用分页数据
                const modelCounts = {};
                this.logs.forEach(log => {
                    const model = log.model || 'unknown';
                    modelCounts[model] = (modelCounts[model] || 0) + 1;
                });
                const sortedModels = Object.entries(modelCounts)
                    .sort(([, a], [, b]) => b - a)
                    .slice(0, 10);
                labels = sortedModels.map(([model]) => model);
                data = sortedModels.map(([, count]) => count);
            }

            this.fullscreenChartInstance = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: '使用次数',
                        data: data,
                        backgroundColor: 'rgba(59, 130, 246, 0.8)',
                        borderColor: 'rgba(59, 130, 246, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                stepSize: 1,
                                font: { size: 14 }
                            }
                        },
                        x: {
                            ticks: {
                                maxRotation: 45,
                                minRotation: 45,
                                font: { size: 14 }
                            }
                        }
                    },
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    return `使用次数: ${context.parsed.y}`;
                                }
                            }
                        }
                    }
                }
            });
        },

        async createFullscreenTokenTrendChart(ctx) {
            // 使用全局聚合数据
            let labels = [];
            let totalTokens = [];
            let successTokens = [];

            try {
                const params = new URLSearchParams();
                if (this.chartTimeRange) params.append('range', this.chartTimeRange);
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs/stats/tokens-timeline?${params}`);
                const result = await response.json();

                if (result.success && result.data) {
                    const timeline = result.data.sort((a, b) => a.date.localeCompare(b.date));
                    labels = timeline.map(point => point.date);
                    totalTokens = timeline.map(point => point.total || 0);
                    successTokens = timeline.map(point => point.success || 0);
                }
            } catch (error) {
                console.error('Failed to fetch tokens timeline for fullscreen:', error);
                // 降级到使用分页数据
                const tokenByTime = {};
                this.logs.forEach(log => {
                    if (log.created_at && log.tokens_used) {
                        const date = new Date(log.created_at).toISOString().split('T')[0];
                        if (!tokenByTime[date]) {
                            tokenByTime[date] = { total: 0, success: 0 };
                        }
                        tokenByTime[date].total += parseInt(log.tokens_used) || 0;
                        if (log.status_code === 200) {
                            tokenByTime[date].success += parseInt(log.tokens_used) || 0;
                        }
                    }
                });
                const sortedDates = Object.keys(tokenByTime).sort();
                labels = sortedDates;
                totalTokens = sortedDates.map(date => tokenByTime[date].total);
                successTokens = sortedDates.map(date => tokenByTime[date].success);
            }

            this.fullscreenChartInstance = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: '总Token数',
                        data: totalTokens,
                        borderColor: 'rgba(59, 130, 246, 1)',
                        backgroundColor: 'rgba(59, 130, 246, 0.1)',
                        fill: true,
                        tension: 0.4
                    }, {
                        label: '成功Token数',
                        data: successTokens,
                        borderColor: 'rgba(16, 185, 129, 1)',
                        backgroundColor: 'rgba(16, 185, 129, 0.1)',
                        fill: true,
                        tension: 0.4
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: { font: { size: 14 } }
                        },
                        x: {
                            ticks: { font: { size: 14 } }
                        }
                    },
                    plugins: {
                        legend: {
                            position: 'top',
                            labels: { font: { size: 16 } }
                        }
                    }
                }
            });
        },

        createFullscreenGroupTokenChart(ctx) {
            // 按服务商分组统计Token
            const groupTokens = {};
            this.logs.forEach(log => {
                if (log.provider_group && log.tokens_used) {
                    const group = log.provider_group;
                    if (!groupTokens[group]) {
                        groupTokens[group] = { total: 0, success: 0 };
                    }
                    groupTokens[group].total += parseInt(log.tokens_used) || 0;
                    if (log.status_code === 200) {
                        groupTokens[group].success += parseInt(log.tokens_used) || 0;
                    }
                }
            });

            const labels = Object.keys(groupTokens);
            const totalData = labels.map(group => groupTokens[group].total);
            const successData = labels.map(group => groupTokens[group].success);

            this.fullscreenChartInstance = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: '总Token数',
                        data: totalData,
                        backgroundColor: 'rgba(59, 130, 246, 0.8)',
                        borderColor: 'rgba(59, 130, 246, 1)',
                        borderWidth: 1
                    }, {
                        label: '成功Token数',
                        data: successData,
                        backgroundColor: 'rgba(16, 185, 129, 0.8)',
                        borderColor: 'rgba(16, 185, 129, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: { font: { size: 14 } }
                        },
                        x: {
                            ticks: { font: { size: 14 } }
                        }
                    },
                    plugins: {
                        legend: {
                            position: 'top',
                            labels: { font: { size: 16 } }
                        }
                    }
                }
            });
        },

        // 实际的图表创建方法 - 使用全局聚合数据
        async createStatusChart() {
            const canvas = document.getElementById('statusChart');
            if (!canvas) return;

            const ctx = canvas.getContext('2d');

            // 从全局聚合数据获取状态分布
            let successCount = 0;
            let errorCount = 0;

            try {
                // 构建查询参数
                const params = new URLSearchParams();
                if (this.chartTimeRange) params.append('range', this.chartTimeRange);
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs/stats/status?${params}`);
                const result = await response.json();

                if (result.success && result.data) {
                    successCount = result.data.success || 0;
                    errorCount = result.data.error || 0;
                }
            } catch (error) {
                console.error('Failed to fetch status stats:', error);
                // 降级到使用现有统计数据
                successCount = this.successRequests || 0;
                errorCount = this.errorRequests || 0;
            }

            const labels = ['成功请求', '失败请求'];
            const data = [successCount, errorCount];
            const colors = ['#10B981', '#EF4444']; // 绿色和红色

            this.chartInstances.status = new Chart(ctx, {
                type: 'doughnut',
                data: {
                    labels: labels,
                    datasets: [{
                        data: data,
                        backgroundColor: colors,
                        borderWidth: 2,
                        borderColor: '#ffffff'
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: {
                            position: 'bottom',
                            labels: {
                                padding: 20,
                                usePointStyle: true
                            }
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                    const percentage = total > 0 ? ((context.parsed * 100) / total).toFixed(1) : 0;
                                    return `${context.label}: ${context.parsed} (${percentage}%)`;
                                }
                            }
                        }
                    }
                }
            });
        },

        async createModelChart() {
            const canvas = document.getElementById('modelChart');
            if (!canvas) {
                console.error('Model chart canvas not found');
                return;
            }

            const ctx = canvas.getContext('2d');
            console.log('Creating model chart...');

            // 从全局聚合数据获取模型使用分布
            let labels = [];
            let data = [];

            try {
                // 构建查询参数
                const params = new URLSearchParams();
                if (this.chartTimeRange) params.append('range', this.chartTimeRange);
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs/stats/models?${params}`);
                const result = await response.json();

                if (result.success && result.stats) {
                    // 取前10个最常用的模型
                    const sortedModels = result.stats
                        .sort((a, b) => (b.total_requests || 0) - (a.total_requests || 0))
                        .slice(0, 10);

                    labels = sortedModels.map(stat => stat.model || 'unknown');
                    data = sortedModels.map(stat => stat.total_requests || 0);
                } else {
                    console.warn('API响应格式错误:', result);
                    throw new Error('API响应格式错误');
                }
            } catch (error) {
                console.error('Failed to fetch model stats:', error);
                // 降级到使用分页数据进行粗略统计
                const modelCounts = {};
                this.logs.forEach(log => {
                    const model = log.model || 'unknown';
                    modelCounts[model] = (modelCounts[model] || 0) + 1;
                });

                if (Object.keys(modelCounts).length > 0) {
                    const sortedModels = Object.entries(modelCounts)
                        .sort(([, a], [, b]) => b - a)
                        .slice(0, 10);
                    labels = sortedModels.map(([model]) => model);
                    data = sortedModels.map(([, count]) => count);
                } else {
                    // 如果完全没有数据，显示占位数据
                    labels = ['无数据'];
                    data = [0];
                }
            }

            // 确保至少有一些数据显示
            if (labels.length === 0) {
                labels = ['无数据'];
                data = [0];
            }

            console.log('Model chart data:', { labels, data });

            this.chartInstances.model = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: '使用次数',
                        data: data,
                        backgroundColor: 'rgba(59, 130, 246, 0.8)',
                        borderColor: 'rgba(59, 130, 246, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true,
                            ticks: {
                                stepSize: 1
                            }
                        },
                        x: {
                            ticks: {
                                maxRotation: 45,
                                minRotation: 45
                            }
                        }
                    },
                    plugins: {
                        legend: {
                            display: false
                        },
                        tooltip: {
                            callbacks: {
                                label: function (context) {
                                    return `使用次数: ${context.parsed.y}`;
                                }
                            }
                        }
                    }
                }
            });

            console.log('Model chart created successfully');
        },

        async createTokenTrendChart() {
            const canvas = document.getElementById('tokenTrendChart');
            if (!canvas) return;

            const ctx = canvas.getContext('2d');

            // 从全局聚合数据获取Token时间线
            let labels = [];
            let totalTokens = [];
            let successTokens = [];

            try {
                // 构建查询参数
                const params = new URLSearchParams();
                if (this.chartTimeRange) params.append('range', this.chartTimeRange);
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs/stats/tokens-timeline?${params}`);
                const result = await response.json();

                if (result.success && result.data) {
                    const timeline = result.data.sort((a, b) => a.date.localeCompare(b.date));
                    labels = timeline.map(point => point.date);
                    totalTokens = timeline.map(point => point.total || 0);
                    successTokens = timeline.map(point => point.success || 0);
                }
            } catch (error) {
                console.error('Failed to fetch tokens timeline:', error);
                // 降级到使用分页数据进行粗略统计
                const tokenByTime = {};
                this.logs.forEach(log => {
                    if (log.created_at && log.tokens_used) {
                        const date = new Date(log.created_at).toISOString().split('T')[0];
                        if (!tokenByTime[date]) {
                            tokenByTime[date] = { total: 0, success: 0 };
                        }
                        tokenByTime[date].total += parseInt(log.tokens_used) || 0;
                        if (log.status_code === 200) {
                            tokenByTime[date].success += parseInt(log.tokens_used) || 0;
                        }
                    }
                });
                const sortedDates = Object.keys(tokenByTime).sort();
                labels = sortedDates;
                totalTokens = sortedDates.map(date => tokenByTime[date].total);
                successTokens = sortedDates.map(date => tokenByTime[date].success);
            }

            this.chartInstances.tokenTrend = new Chart(ctx, {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        label: '总Token数',
                        data: totalTokens,
                        borderColor: 'rgba(59, 130, 246, 1)',
                        backgroundColor: 'rgba(59, 130, 246, 0.1)',
                        fill: true,
                        tension: 0.4
                    }, {
                        label: '成功Token数',
                        data: successTokens,
                        borderColor: 'rgba(16, 185, 129, 1)',
                        backgroundColor: 'rgba(16, 185, 129, 0.1)',
                        fill: true,
                        tension: 0.4
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true
                        }
                    },
                    plugins: {
                        legend: {
                            position: 'top'
                        }
                    }
                }
            });
        },

        async createGroupTokenChart() {
            const canvas = document.getElementById('groupTokenChart');
            if (!canvas) return;

            const ctx = canvas.getContext('2d');

            // 从全局聚合数据获取分组Token统计
            let labels = [];
            let totalData = [];
            let successData = [];

            try {
                // 构建查询参数
                const params = new URLSearchParams();
                if (this.chartTimeRange) params.append('range', this.chartTimeRange);
                if (this.filters.proxyKeyName) params.append('proxyKey', this.filters.proxyKeyName);
                if (this.filters.providerGroup) params.append('group', this.filters.providerGroup);
                if (this.filters.model) params.append('model', this.filters.model);
                if (this.filters.stream) params.append('type', this.filters.stream);

                const response = await fetch(`/admin/logs/stats/group-tokens?${params}`);
                const result = await response.json();

                if (result.success && result.data) {
                    const groups = result.data.slice(0, 10); // 取前10个分组
                    labels = groups.map(group => group.group || '-');
                    totalData = groups.map(group => group.total || 0);
                    successData = groups.map(group => group.success || 0);
                }
            } catch (error) {
                console.error('Failed to fetch group tokens stats:', error);
                // 降级到使用分页数据进行粗略统计
                const groupTokens = {};
                this.logs.forEach(log => {
                    if (log.provider_group && log.tokens_used) {
                        const group = log.provider_group;
                        if (!groupTokens[group]) {
                            groupTokens[group] = { total: 0, success: 0 };
                        }
                        groupTokens[group].total += parseInt(log.tokens_used) || 0;
                        if (log.status_code === 200) {
                            groupTokens[group].success += parseInt(log.tokens_used) || 0;
                        }
                    }
                });
                labels = Object.keys(groupTokens);
                totalData = labels.map(group => groupTokens[group].total);
                successData = labels.map(group => groupTokens[group].success);
            }

            this.chartInstances.groupToken = new Chart(ctx, {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        label: '总Token数',
                        data: totalData,
                        backgroundColor: 'rgba(59, 130, 246, 0.8)',
                        borderColor: 'rgba(59, 130, 246, 1)',
                        borderWidth: 1
                    }, {
                        label: '成功Token数',
                        data: successData,
                        backgroundColor: 'rgba(16, 185, 129, 0.8)',
                        borderColor: 'rgba(16, 185, 129, 1)',
                        borderWidth: 1
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    scales: {
                        y: {
                            beginAtZero: true
                        }
                    },
                    plugins: {
                        legend: {
                            position: 'top'
                        }
                    }
                }
            });
        }
    }
}