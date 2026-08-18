
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HUIOTClinicMS.Models;
using HUIOTClinicMS.Data;

public class DormBlocksController : Controller
{
    private readonly ApplicationDbContext _context;

    public DormBlocksController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: DORMBLOCKS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.DormBlocks.ToListAsync());
    }

    // GET: DORMBLOCKS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var dormblock = await _context.DormBlocks
            .FirstOrDefaultAsync(m => m.ID == id);
        if (dormblock == null)
        {
            return NotFound();
        }

        return View(dormblock);
    }

    // GET: DORMBLOCKS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: DORMBLOCKS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ID,BlockName")] DormBlock dormblock)
    {
        if (ModelState.IsValid)
        {
            _context.Add(dormblock);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(dormblock);
    }

    // GET: DORMBLOCKS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var dormblock = await _context.DormBlocks.FindAsync(id);
        if (dormblock == null)
        {
            return NotFound();
        }
        return View(dormblock);
    }

    // POST: DORMBLOCKS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,BlockName")] DormBlock dormblock)
    {
        if (id != dormblock.ID)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(dormblock);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DormBlockExists(dormblock.ID))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(dormblock);
    }

    // GET: DORMBLOCKS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var dormblock = await _context.DormBlocks
            .FirstOrDefaultAsync(m => m.ID == id);
        if (dormblock == null)
        {
            return NotFound();
        }

        return View(dormblock);
    }

    // POST: DORMBLOCKS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var dormblock = await _context.DormBlocks.FindAsync(id);
        if (dormblock != null)
        {
            _context.DormBlocks.Remove(dormblock);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool DormBlockExists(int? id)
    {
        return _context.DormBlocks.Any(e => e.ID == id);
    }
}
