import * as pdfjsLib from 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/4.9.155/pdf.min.mjs';

let pdfDoc = null;
let pageNum = 1;
let pageRendering = false;
let pageNumPending = null;
let currentScale = 1.0; // Default scale

function renderPdfInDiv(container, pdfData) {
    // Clear any existing content in the container
    container.innerHTML = "";

    // Decode the base64 PDF data
    const pdfBinary = atob(pdfData);

    // Initialize PDF.js
    const loadingTask = pdfjsLib.getDocument({ data: pdfBinary });

    loadingTask.promise.then(pdf => {
        pdfDoc = pdf;
        document.getElementById('page_count').textContent = pdfDoc.numPages;

        // Render the first page
        pageNum = 1;
        renderPage(pageNum, container);
    }).catch(error => {
        console.error("Error rendering PDF:", error);
        container.innerHTML = "<p>Failed to load PDF.</p>";
    });
}

function renderPage(num, container) {
    pageRendering = true;

    // Get the page
    pdfDoc.getPage(num).then(page => {
        const canvas = document.createElement("canvas");
        const context = canvas.getContext("2d");
        const viewport = page.getViewport({ scale: currentScale });

        canvas.width = viewport.width;
        canvas.height = viewport.height;

        container.innerHTML = ""; // Clear previous content
        container.appendChild(canvas);

        const renderContext = {
            canvasContext: context,
            viewport: viewport
        };

        const renderTask = page.render(renderContext);

        renderTask.promise.then(() => {
            pageRendering = false;

            if (pageNumPending !== null) {
                renderPage(pageNumPending, container);
                pageNumPending = null;
            }
        });
    });
}

function queueRenderPage(num, container) {
    if (pageRendering) {
        pageNumPending = num;
    } else {
        renderPage(num, container);
    }
}

function onPrevPage(container) {
    if (pageNum <= 1) {
        return;
    }
    pageNum--;
    queueRenderPage(pageNum, container);
}

function onNextPage(container) {
    if (pageNum >= pdfDoc.numPages) {
        return;
    }
    pageNum++;
    queueRenderPage(pageNum, container);
}

function onZoomIn(container) {
    currentScale += 0.2;
    renderPage(pageNum, container);
}

function onZoomOut(container) {
    if (currentScale > 0.4) {
        currentScale -= 0.2;
        renderPage(pageNum, container);
    }
}

function onPrintPdf() {
    if (!pdfDoc) {
        console.error("PDF document is not loaded.");
        return;
    }

    pdfDoc.getPage(pageNum).then(page => {
        const canvas = document.createElement("canvas");
        const context = canvas.getContext("2d");
        const viewport = page.getViewport({ scale: 1.5 });

        canvas.width = viewport.width;
        canvas.height = viewport.height;

        const renderContext = {
            canvasContext: context,
            viewport: viewport
        };

        page.render(renderContext).promise.then(() => {
            const imgData = canvas.toDataURL();

            // Create a hidden iframe
            const iframe = document.createElement('iframe');
            iframe.style.position = 'absolute';
            iframe.style.left = '-9999px';
            document.body.appendChild(iframe);

            const iframeDoc = iframe.contentDocument || iframe.contentWindow.document;
            iframeDoc.open();
            iframeDoc.write(`
                <html>
                    <head>
                        <title>Print PDF</title>
                        <style>
                            body { margin: 0; display: flex; justify-content: center; align-items: center; height: 100vh; }
                            img { max-width: 100%; max-height: 100%; }
                        </style>
                    </head>
                    <body>
                        <img src="${imgData}" />
                    </body>
                </html>
            `);
            iframeDoc.close();

            // Wait for the image to load before printing
            iframe.onload = () => {
                iframe.contentWindow.focus();
                iframe.contentWindow.print();
                document.body.removeChild(iframe);
            };
        });
    }).catch(error => {
        console.error("Error while rendering page for printing:", error);
    });
}

function ongetPageCount() {
    var NumberOfPages = pdfDoc.numPages;
    return NumberOfPages;
}

// Expose the functions to the global scope
window.renderPdfInDiv = renderPdfInDiv;
window.onPrevPage = onPrevPage;
window.onNextPage = onNextPage;
window.onZoomIn = onZoomIn;
window.onZoomOut = onZoomOut;
window.onPrintPdf = onPrintPdf;
window.ongetPageCount = ongetPageCount;

window.pdfDoc = pdfDoc;
window.pageNum = pageNum;
window.pageRendering = pageRendering;
window.pageNumPending = pageNumPending;
window.currentScale = currentScale;