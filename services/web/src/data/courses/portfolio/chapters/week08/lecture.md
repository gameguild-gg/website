# **Week 8: Presenting Projects Visually**

## **Introduction**

In order to better communicate your work, you should a visual pitch that communicates your skills, creativity, and professionalism. High-quality **graphs, diagrams, screenshots, GIFs, and videos** make a significant difference in how your work is perceived. This week, we’ll explore **best practices** for capturing, optimizing, and presenting visual assets for your portfolio. 

---

## **Using UML and Diagramming Tools to Express Complex Ideas**

Communicating complex game systems, workflows, or architecture visually can greatly enhance clarity. Instead of lengthy explanations, consider using UML (Unified Modeling Language) diagrams and flowcharts. These tools allow you to **illustrate game mechanics, object interactions, AI behavior, and system architectures** efficiently.

### **Recommended Tools**

- **[Mermaid.js](https://mermaid.live/)** – Integrates directly into markdown and supports sequence diagrams, flowcharts, and class diagrams.
- **[Diagram.codes](https://diagram.codes/)** – A versatile diagramming tool with live editing.
- **[Code2Flow](https://www.code2flow.com/)** – Automatically generates flowcharts from pseudo-code.
- **[SequenceDiagram.org](https://sequencediagram.org/)** – Great for creating **sequence diagrams** to illustrate event-driven interactions.
- **[Draw.io (diagrams.net)](https://www.diagrams.net/)** – A powerful drag-and-drop diagramming tool for system architecture and class diagrams.

### **Examples of Use Cases**
| **Use Case** | **Recommended Diagram** | **Tool**                       |
|-------------|----------------------|--------------------------------|
| **Game State Transitions** | State Machine Diagram | Mermaid, Draw.io               |
| **AI Behavior Trees** | Flowchart | Code2Flow, Diagram.codes       |
| **Player & Enemy Interactions** | Sequence Diagram | SequenceDiagram.org            |
| **Class Structures in Code** | Class Diagram | Draw.io, Mermaid               |
| **Data Bases & Networking** | Entity-Relationship Diagram | Mermaid, Draw.io |

Visualizing ideas helps both **team collaboration and portfolio presentation**, making complex mechanics and systems more digestible.

## **Taking Professional-Quality Screenshots and GIFs**

### **Screenshots: What to Capture?**

- Focus on key moments that highlight gameplay, mechanics, or unique visuals.
- Ensure **good framing**, avoiding unnecessary UI clutter.
- Use a **consistent aspect ratio** that aligns with modern screen sizes (16:9, 4:3, or even square formats for social media).
- Consider adjusting brightness, contrast, and color balance to make the visuals pop.

### **GIFs vs. Video Clips: Pros and Cons**

| Format | Pros | Cons                                                             |
|--------|------|------------------------------------------------------------------|
| **GIFs** | Quick to load, loop automatically, work well in social media and forums | Large file size, limited color range, no audio                   |
| **Videos** | Higher quality, better compression, can include sound (if needed) | Requires a player, may not autoplay everywhere or mobile devices |

### **Creating GIFs from Videos**
If you need to convert a video into a GIF, consider:
- **FFmpeg:** A powerful open-source tool to convert and optimize GIFs from video clips.  
  ffmpeg -i input.mp4 -vf "fps=15,scale=640:-1:flags=lanczos" -c:v gif output.gif
- **Adobe Cloud Converter**: A simple online tool for converting and optimizing GIFs.
- **Ezgif**: A web-based GIF optimization tool that allows cropping, resizing, and reducing file sizes.

---

## **Designing Eye-Catching Thumbnails and Banners**

### **Thumbnails for Portfolio Cards**
- Keep it **clean and readable**: Avoid too much detail that won’t be visible in small sizes.
- Highlight a **recognizable visual** (e.g., main character, key UI element, or gameplay highlight).
- Add a **subtle overlay or text** to indicate the project’s title or a short descriptor.

### **Banners and Backgrounds**
- Use **subtle, non-distracting visuals** for backgrounds.
- If using a **video banner**, keep it **short, loopable, and without audio** (e.g., 5-10 seconds).
- Ensure the banner complements your **portfolio layout** without overshadowing other content.

---

## **Best Practices for Videos on Webpages**

### **Autoplay Without Sound**
- Videos should **play in the background without audio**.
- To enable **muted autoplay**, use the following HTML snippet:
``` html
  <video autoplay loop muted playsinline>  
      <source src="video.mp4" type="video/mp4">  
  </video>  
```
- The `playsinline` attribute ensures the video plays within mobile browsers instead of going fullscreen.

### **Optimizing Video File Size**
- Use **FFmpeg** to compress videos without significant quality loss:

``` sh
ffmpeg -i input.mp4 -vcodec libx264 -crf 23 -preset slow output.mp4
```
- 
- `crf 23`: Adjusts quality (lower values mean higher quality).
- `preset slow`: Optimizes compression while balancing performance.

---

## **Different Video Purposes & Best Practices**

| **Purpose** | **Best Practices** |
|------------|-------------------|
| **Call-to-action teaser** (e.g., a project card) | Short, looped, no audio, focus on a single feature |
| **Feature explanation in a blog post** | Slightly longer, may include annotations or captions |
| **Background banner** | Subtle motion, loop seamlessly, muted |
| **Gameplay showcase** | Highlight engaging gameplay, keep cuts dynamic |

---

## **Writing Effective Captions for Visual Assets**
- Keep captions **concise** but informative.
- **Highlight key features** showcased in the visual.
- Use **action-oriented language**: Instead of “Main character running,” say **“Dynamic parkour movement system in action.”**
- Avoid redundancy—if the visual already conveys the idea, keep text minimal.

---

!!! quiz
{
"title": "Choosing the Right Visual Format",
"question": "Which format is best for a short, looping preview of a game mechanic on a project card?",
"options": [
"A high-resolution screenshot",
"A full-length gameplay video",
"A GIF or silent autoplaying video",
"A text-based description"
],
"answers": ["A GIF or silent autoplaying video"]
}
!!!

By applying these techniques, you’ll create a visually compelling portfolio that enhances your projects and engages potential employers or collaborators.  
