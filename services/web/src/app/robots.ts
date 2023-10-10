import {MetadataRoute} from "next";

async function robots(): Promise<MetadataRoute.Robots> {
    const host = "";

    return {
        rules: [
            {
                userAgent: "*",
                allow: "/",
                disallow: "/dashboard/",
            },
        ],
        sitemap: `https://${host}/sitemap.xml`,
    };
}

export default robots;
