import { Calendar, RefreshCw, User, ChevronDown, ChevronUp, Newspaper } from 'lucide-react';
import React, { useState, useEffect, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { BrowserOpenURL } from '../../wailsjs/runtime/runtime';

interface NewsItem {
    title: string;
    excerpt: string;
    url: string;
    date: string;
    author: string;
    imageUrl?: string;
    source?: 'hytale' | 'hyprism';
}

interface NewsPreviewProps {
    getNews: (count: number) => Promise<NewsItem[]>
}

type NewsFilter = 'all' | 'hytale' | 'hyprism';

export const NewsPreview: React.FC<NewsPreviewProps> = ({ getNews }) => {
    const { t } = useTranslation();
    const [news, setNews] = useState<NewsItem[]>([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const [limit, setLimit] = useState(6);
    const [filter, setFilter] = useState<NewsFilter>('all');
    const [isMinimized, setIsMinimized] = useState(false);
    const listRef = useRef<HTMLDivElement>(null);

    const fetchNews = async (count: number, reset = false) => {
        setLoading(true);
        setError(null);
        try {
            const items = await getNews(count);
            setNews((prev) => {
                if (reset) return items;
                const seen = new Map<string, NewsItem>();
                prev.forEach((item) => seen.set(item.url + item.title, item));
                (items || []).forEach((item) => seen.set(item.url + item.title, item));
                return Array.from(seen.values());
            });
        } catch (err) {
            setError(err instanceof Error ? err.message : 'Failed to fetch news');
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        fetchNews(limit, limit === 6 && news.length === 0);
    }, [limit]);

    useEffect(() => {
        const interval = setInterval(() => {
            fetchNews(limit);
        }, 30000);
        return () => clearInterval(interval);
    }, [limit]);

    const openLink = (url: string) => {
        BrowserOpenURL(url);
    };

    const handleScroll = () => {
        if (!listRef.current || loading) return;
        const { scrollTop, scrollHeight, clientHeight } = listRef.current;
        if (scrollHeight - scrollTop - clientHeight < 120) {
            setLimit((prev) => prev + 6);
        }
    };

    // Filter news by source
    const filteredNews = filter === 'all' 
        ? news 
        : news.filter(item => item.source === filter);

    return (
        <div className='flex flex-col gap-y-2 max-w-sm'>
            <div className='flex justify-between items-center'>
                <div className='flex items-center gap-2'>
                    <Newspaper size={18} className='text-white' />
                    <h2 className='text-sm font-bold text-white'>{t('News')}</h2>
                </div>
                <div className='flex gap-3 items-center ml-4'>
                    {!isMinimized && (
                        <>
                            <button
                                onClick={() => setFilter('all')}
                                className={`px-3 py-1 text-xs rounded-lg transition-all ${
                                    filter === 'all'
                                        ? 'bg-[#FFA845] text-black font-medium'
                                        : 'bg-white/10 text-white/70 hover:bg-white/20'
                                }`}
                            >
                                {t('All')}
                            </button>
                            <button
                                onClick={() => setFilter('hytale')}
                                className={`px-3 py-1 text-xs rounded-lg transition-all ${
                                    filter === 'hytale'
                                        ? 'bg-[#FFA845] text-black font-medium'
                                        : 'bg-white/10 text-white/70 hover:bg-white/20'
                                }`}
                            >
                                {t('Hytale')}
                            </button>
                            <button
                                onClick={() => setFilter('hyprism')}
                                className={`px-3 py-1 text-xs rounded-lg transition-all ${
                                    filter === 'hyprism'
                                        ? 'bg-[#FFA845] text-black font-medium'
                                        : 'bg-white/10 text-white/70 hover:bg-white/20'
                                }`}
                            >
                                {t('HyPrism')}
                            </button>
                        </>
                    )}
                    <button
                        onClick={() => setIsMinimized(!isMinimized)}
                        className='p-1.5 rounded-lg bg-white/10 hover:bg-white/20 text-white transition-all'
                        title={isMinimized ? t('Expand') : t('Minimize')}
                    >
                        {isMinimized ? <ChevronDown size={14} /> : <ChevronUp size={14} />}
                    </button>
                </div>
            </div>

            {!isMinimized && (
                loading ? (
                    <div className="flex items-center justify-center py-4">
                        <RefreshCw size={24} className="text-[#FFA845] animate-spin" />
                    </div>
                ) : error ? (
                    <div className="flex items-center justify-center py-8">
                        <div className="text-center">
                            <p className="text-red-400 mb-3 text-sm">{error}</p>
                            <button
                                onClick={fetchNews}
                                className="px-4 py-2 bg-white/10 hover:bg-white/20 rounded-lg transition-colors text-sm"
                            >
                                {t('Try Again')}
                            </button>
                        </div>
                    </div>
                ) : filteredNews.length > 0 ? (
                    <div ref={listRef} onScroll={handleScroll} className='flex flex-col gap-2 max-h-72 overflow-y-auto pr-1'>
                        {filteredNews.map((item) => (
                            <button
                                key={item.url + item.title}
                                onClick={() => openLink(item.url)}
                                disabled={loading}
                                className='flex gap-2 group hover:bg-white/5 p-2 rounded-lg transition-all cursor-pointer text-left w-full glass'
                            >
                                {item.imageUrl && (
                                    <img
                                        src={item.imageUrl}
                                        alt={item.title}
                                        className={`object-cover rounded-md flex-shrink-0 group-hover:scale-105 transition-transform ${item.source === 'hyprism' ? 'w-10 h-10' : 'w-20 h-14'}`}
                                    />
                                )}
                                <div className='flex flex-col justify-center min-w-0'>
                                    <p className='text-[#FFA845] group-hover:underline text-xs font-medium line-clamp-2 mb-0.5'>
                                        {item.title}
                                    </p>
                                    <p className='text-white/70 text-xs line-clamp-2 mb-1'>{item.excerpt}</p>
                                    <div className='flex gap-x-2 items-center text-white/50 text-xs'>
                                        <span className='flex items-center gap-1'><User size='10' />{item.author}</span>
                                        <span className='flex items-center gap-1'><Calendar size='10' />{item.date}</span>
                                    </div>
                                </div>
                            </button>
                        ))}
                        {loading && (
                            <div className='flex items-center justify-center py-2 text-xs text-white/50'>
                                <RefreshCw size={12} className='animate-spin' />
                            </div>
                        )}
                        <button
                            onClick={() => openLink("https://hytale.com/news")}
                            className='w-full font-semibold hover:underline cursor-pointer text-[#FFA845] text-xs mt-0.5'>
                            {t('Read more on hytale.com')} →
                        </button>
                    </div>
                ) : (
                    <div className="flex items-center justify-center py-4">
                        <p className="text-white/50 text-sm">{t('No news found')}</p>
                    </div>
                )
            )}

        </div>
    );
};
