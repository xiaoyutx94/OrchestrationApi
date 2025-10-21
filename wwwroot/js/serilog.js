// Alpine.js组件：系统日志管理
function serilogManagement() {
    return {
        // 状态数据
        logs: [],
        levels: [],
        statistics: {
            totalLogs: 0,
            logsByLevel: {},
            last24Hours: 0,
            lastHour: 0,
            databaseSize: 0,
            earliestLog: null,
            latestLog: null
        },
        filters: {
            level: '',
            keyword: '',
            startTime: '',
            endTime: ''
        },
        currentPage: 1,
        pageSize: 10,
        totalLogs: 0,
        totalPages: 0,
        loading: false,
        autoRefresh: false,
        autoRefreshInterval: null,
        selectedLog: null,
        showDetailModal: false,
        showCleanupModal: false,
        cleanupDays: 30,
        levelPieChart: null,
        levelBarChart: null,

        // 初始化
        async init() {
            this.loadLevels();
            this.loadLogs();
            // 先加载统计数据
            await this.loadStatistics();
            // 延迟初始化图表，确保DOM完全加载和数据已获取
            setTimeout(() => {
                this.initCharts();
            }, 500);
        },

        // API请求封装
        async fetchApi(url, options = {}) {
            const token = localStorage.getItem('authToken');
            const defaultOptions = {
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            };
            const response = await fetch(url, { ...defaultOptions, ...options });
            
            // 处理401未授权错误
            if (response.status === 401) {
                console.warn('未授权，跳转到登录页');
                localStorage.removeItem('authToken');
                window.location.href = '/login.html';
                return response;
            }
            
            return response;
        },

        // 加载统计信息
        async loadStatistics() {
            try {
                const response = await this.fetchApi('/admin/serilog/statistics');
                if (response.ok) {
                    const data = await response.json();
                    
                    // 直接赋值，保持原始数据结构，避免引起 x-show 闪烁
                    this.statistics.totalLogs = data.totalLogs || 0;
                    this.statistics.logsByLevel = data.logsByLevel || {};
                    this.statistics.last24Hours = data.last24Hours || 0;
                    this.statistics.lastHour = data.lastHour || 0;
                    this.statistics.databaseSize = data.databaseSize || 0;
                    this.statistics.earliestLog = data.earliestLog || null;
                    this.statistics.latestLog = data.latestLog || null;
                    
                    // 如果图表已经初始化，则更新图表
                    if (this.levelPieChart && this.levelBarChart) {
                        this.updateCharts();
                    }
                } else {
                    console.error('加载统计信息失败');
                }
            } catch (error) {
                console.error('加载统计信息出错:', error);
            }
        },

        // 加载日志级别列表
        async loadLevels() {
            try {
                const response = await this.fetchApi('/admin/serilog/levels');
                if (response.ok) {
                    this.levels = await response.json();
                }
            } catch (error) {
                console.error('加载日志级别失败:', error);
            }
        },

        // 加载日志列表
        async loadLogs() {
            try {
                const params = new URLSearchParams({
                    page: this.currentPage,
                    pageSize: this.pageSize
                });

                if (this.filters.level) params.append('level', this.filters.level);
                if (this.filters.keyword) params.append('keyword', this.filters.keyword);
                if (this.filters.startTime) params.append('startTime', new Date(this.filters.startTime).toISOString());
                if (this.filters.endTime) params.append('endTime', new Date(this.filters.endTime).toISOString());

                const response = await this.fetchApi(`/admin/serilog?${params}`);

                if (response.ok) {
                    const data = await response.json();
                    this.logs = data.logs || [];
                    this.totalLogs = data.total || 0;
                    this.totalPages = data.totalPages || 0;
                } else {
                    console.error('加载日志失败');
                }
            } catch (error) {
                console.error('加载日志出错:', error);
            }
        },

        // 应用筛选
        applyFilters() {
            this.currentPage = 1;
            this.loadLogs();
        },

        // 重置筛选
        resetFilters() {
            this.filters = {
                level: '',
                keyword: '',
                startTime: '',
                endTime: ''
            };
            this.currentPage = 1;
            this.loadLogs();
        },

        // 刷新数据
        refreshLogs() {
            // 只刷新日志列表，不刷新统计数据（避免图表区域跳动）
            this.loadLogs();
        },

        // 切换自动刷新
        toggleAutoRefresh() {
            this.autoRefresh = !this.autoRefresh;
            if (this.autoRefresh) {
                this.autoRefreshInterval = setInterval(() => {
                    this.refreshLogs();
                }, 30000); // 每30秒刷新一次
            } else {
                if (this.autoRefreshInterval) {
                    clearInterval(this.autoRefreshInterval);
                    this.autoRefreshInterval = null;
                }
            }
        },

        // 分页跳转
        goToPage(page) {
            if (page >= 1 && page <= this.totalPages) {
                this.currentPage = page;
                this.loadLogs();
            }
        },

        // 显示日志详情
        showLogDetail(log) {
            this.selectedLog = log;
            this.showDetailModal = true;
        },

        // 清理旧日志
        async cleanupOldLogs() {
            const days = parseInt(this.cleanupDays);

            if (isNaN(days) || days < 1) {
                alert('请输入有效的天数');
                return;
            }

            const beforeDate = new Date();
            beforeDate.setDate(beforeDate.getDate() - days);

            if (!confirm(`确定要删除 ${days} 天之前的日志吗？此操作不可恢复！`)) {
                return;
            }

            try {
                const response = await this.fetchApi(`/admin/serilog?before=${beforeDate.toISOString()}`, {
                    method: 'DELETE'
                });

                if (response.ok) {
                    const result = await response.json();
                    alert(result.message || '删除成功');
                    this.showCleanupModal = false;
                    this.refreshLogs();
                } else {
                    const error = await response.json();
                    alert('删除失败: ' + (error.message || '未知错误'));
                }
            } catch (error) {
                console.error('删除日志失败:', error);
                alert('删除失败: ' + error.message);
            }
        },

        // 初始化图表
        initCharts() {
            const pieCtx = document.getElementById('levelPieChart');
            const barCtx = document.getElementById('levelBarChart');

            if (!pieCtx || !barCtx) {
                console.warn('图表容器未找到，稍后重试');
                setTimeout(() => this.initCharts(), 500);
                return;
            }

            // 准备初始数据
            const labels = this.statistics.logsByLevel ? Object.keys(this.statistics.logsByLevel) : [];
            const data = this.statistics.logsByLevel ? Object.values(this.statistics.logsByLevel) : [];

            if (pieCtx && !this.levelPieChart) {
                this.levelPieChart = new Chart(pieCtx, {
                        type: 'pie',
                        data: {
                            labels: labels,
                            datasets: [{
                                data: data,
                                backgroundColor: [
                                    '#ef4444', // Error - red
                                    '#f97316', // Warning - orange
                                    '#3b82f6', // Information - blue
                                    '#8b5cf6', // Debug - purple
                                    '#6b7280', // Verbose - gray
                                ],
                                borderWidth: 2,
                                borderColor: '#fff'
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: {
                                legend: {
                                    position: 'bottom',
                                },
                                tooltip: {
                                    callbacks: {
                                        label: function(context) {
                                            const label = context.label || '';
                                            const value = context.parsed || 0;
                                            const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                            const percentage = ((value / total) * 100).toFixed(1);
                                            return `${label}: ${value} (${percentage}%)`;
                                        }
                                    }
                                }
                            }
                        }
                    });
            }

            if (barCtx && !this.levelBarChart) {
                this.levelBarChart = new Chart(barCtx, {
                        type: 'bar',
                        data: {
                            labels: labels,
                            datasets: [{
                                label: '日志数量',
                                data: data,
                                backgroundColor: [
                                    '#ef4444',
                                    '#f97316',
                                    '#3b82f6',
                                    '#8b5cf6',
                                    '#6b7280',
                                ],
                                borderWidth: 1,
                                borderColor: '#fff'
                            }]
                        },
                        options: {
                            responsive: true,
                            maintainAspectRatio: false,
                            plugins: {
                                legend: {
                                    display: false
                                }
                            },
                            scales: {
                                y: {
                                    beginAtZero: true,
                                    ticks: {
                                        precision: 0
                                    }
                                }
                            }
                        }
                    });
            }
        },

        // 更新图表
        updateCharts() {
            if (!this.statistics.logsByLevel || Object.keys(this.statistics.logsByLevel).length === 0) {
                return;
            }

            const labels = Object.keys(this.statistics.logsByLevel);
            const data = Object.values(this.statistics.logsByLevel);

            if (this.levelPieChart) {
                this.levelPieChart.data.labels = labels;
                this.levelPieChart.data.datasets[0].data = data;
                this.levelPieChart.update();
            }

            if (this.levelBarChart) {
                this.levelBarChart.data.labels = labels;
                this.levelBarChart.data.datasets[0].data = data;
                this.levelBarChart.update();
            }
        },

        // 获取日志级别徽章样式
        getLevelBadgeClass(level) {
            const levelLower = (level || '').toLowerCase();
            const classes = {
                'error': 'bg-red-100 text-red-800',
                'fatal': 'bg-red-100 text-red-800',
                'warning': 'bg-yellow-100 text-yellow-800',
                'information': 'bg-blue-100 text-blue-800',
                'info': 'bg-blue-100 text-blue-800',
                'debug': 'bg-purple-100 text-purple-800',
                'verbose': 'bg-gray-100 text-gray-800'
            };
            return classes[levelLower] || 'bg-gray-100 text-gray-800';
        },

        // 格式化日期时间
        formatDateTime(dateStr) {
            if (!dateStr) return '-';
            const date = new Date(dateStr);
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const hours = String(date.getHours()).padStart(2, '0');
            const minutes = String(date.getMinutes()).padStart(2, '0');
            const seconds = String(date.getSeconds()).padStart(2, '0');
            return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
        },

        // 格式化字节大小
        formatBytes(bytes) {
            if (!bytes || bytes === 0) return '0 B';
            const k = 1024;
            const sizes = ['B', 'KB', 'MB', 'GB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
        }
    };
}
