import type { LatestRatesResponse } from '../api'

interface RatesTableProps {
    data: LatestRatesResponse | null
    selectedCurrency: string
    onSelectCurrency: (currency: string) => void
}

const rateFormatter = new Intl.NumberFormat('en-US', {
    minimumFractionDigits: 4,
    maximumFractionDigits: 6,
})

const dateFormatter = new Intl.DateTimeFormat('en-US', {
    dateStyle: 'medium',
    timeZone: 'UTC',
})

export default function RatesTable({ data, selectedCurrency, onSelectCurrency }: RatesTableProps) {
    if (data === null) {
        return (
            <section className="card">
                <h2>Latest rates</h2>
                <p className="muted">Loading rates…</p>
            </section>
        )
    }

    if (data.date === null) {
        return (
            <section className="card">
                <h2>Latest rates</h2>
                <p className="muted">No rates yet. Run a sync to load data.</p>
            </section>
        )
    }

    return (
        <section className="card">
            <h2>Latest rates</h2>
            <p className="muted">
                Base: 1 {data.baseCurrency} · {dateFormatter.format(new Date(`${data.date}T00:00:00Z`))}
            </p>

            <table>
                <thead>
                    <tr>
                        <th>Currency</th>
                        <th className="num">Rate</th>
                    </tr>
                </thead>
                <tbody>
                    {data.rates.map((item) => (
                        <tr
                            key={item.currency}
                            className={item.currency === selectedCurrency ? 'selected' : undefined}
                        >
                            <td>
                                <button
                                    type="button"
                                    className="link-button"
                                    onClick={() => onSelectCurrency(item.currency)}
                                    aria-pressed={item.currency === selectedCurrency}
                                >
                                    {item.currency}
                                </button>
                            </td>
                            <td className="num">{rateFormatter.format(item.rate)}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </section>
    )
}