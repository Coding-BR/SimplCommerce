window.dashboardChart = {
    chartInstance: null,

    renderSalesChart: function (canvasId, dates, salesData, previousPeriodData = [], showComparison = false) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        // Destroy existing chart if it exists
        if (this.chartInstance) {
            this.chartInstance.destroy();
        }

        const datasets = [{
            label: 'Período Atual',
            data: salesData,
            borderColor: '#0d6efd', // Bootstrap primary color
            backgroundColor: 'rgba(13, 110, 253, 0.1)',
            borderWidth: 2,
            fill: true,
            tension: 0.4 // Smooth curves
        }];

        if (showComparison && previousPeriodData && previousPeriodData.length > 0) {
            datasets.push({
                label: 'Período Anterior',
                data: previousPeriodData,
                borderColor: '#6c757d', // Bootstrap secondary color (gray)
                backgroundColor: 'rgba(108, 117, 125, 0.05)',
                borderWidth: 2,
                borderDash: [5, 5], // Dotted line
                fill: false,
                tension: 0.4
            });
        }

        this.chartInstance = new Chart(ctx, {
            type: 'line',
            data: {
                labels: dates,
                datasets: datasets
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: showComparison // Show legend only if comparing
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                let label = context.dataset.label || '';
                                if (label) {
                                    label += ': ';
                                }
                                if (context.parsed.y !== null) {
                                    label += new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(context.parsed.y);
                                }
                                return label;
                            }
                        }
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: {
                            color: '#f0f0f0'
                        },
                        ticks: {
                            callback: function (value) {
                                return new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL', maximumSignificantDigits: 3 }).format(value);
                            }
                        }
                    },
                    x: {
                        grid: {
                            display: false
                        }
                    }
                }
            }
        });
    }
};
