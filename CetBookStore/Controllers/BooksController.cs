using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using CetBookStore.Data;
using CetBookStore.Models;
using Microsoft.AspNetCore.Authorization;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace CetBookStore.Controllers
{
    public class BooksController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostEnvironment;

        public BooksController(ApplicationDbContext context, IWebHostEnvironment hostEnvironment)
        {
            _context = context;
            _hostEnvironment = hostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.Books.Include(b => b.Category);
            return View(await applicationDbContext.ToListAsync());
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var book = await _context.Books
                .Include(b => b.Category)
                .Include(b => b.Comments)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (book == null) return NotFound();

            return View(book);
        }

        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Id,Title,Description,Author,Publisher,PageCount,Price,IsInSale,PreviousPrice,PublicationDate,CategoryId,ImageFile")] Book book)
        {
            if (ModelState.IsValid)
            {
                if (book.ImageFile != null)
                {
                    var fileExtension = Path.GetExtension(book.ImageFile.FileName);
                    var newFileName = Guid.NewGuid().ToString("N") + fileExtension;

                    book.ImageFile.OpenReadStream().CopyTo(
                        new FileStream(Path.Combine(_hostEnvironment.WebRootPath,
                        "images", newFileName),
                        FileMode.Create));
                        
                    book.ImageUrl = newFileName;  
                    _context.Add(book);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                } 
                else                 
                {
                    ModelState.AddModelError("ImageFile", "Please upload an image.");
                }
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", book.CategoryId);
            return View(book);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var book = await _context.Books.FindAsync(id);
            if (book == null) return NotFound();
            
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", book.CategoryId);
            return View(book);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Description,Author,Publisher,PageCount,Price,IsInSale,PreviousPrice,PublicationDate,CreatedDate,CategoryId,ImageUrl,ImageFile")] Book book)
        {
            if (id != book.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (book.ImageFile != null)
                    {
                        if (!string.IsNullOrEmpty(book.ImageUrl))
                        {
                            var oldImagePath = Path.Combine(_hostEnvironment.WebRootPath, "images", book.ImageUrl);
                            if (System.IO.File.Exists(oldImagePath))
                            {
                                System.IO.File.Delete(oldImagePath);
                            }
                        }

                        var fileExtension = Path.GetExtension(book.ImageFile.FileName);
                        var newFileName = Guid.NewGuid().ToString("N") + fileExtension;
                        var savePath = Path.Combine(_hostEnvironment.WebRootPath, "images", newFileName);

                        using (var image = await Image.LoadAsync(book.ImageFile.OpenReadStream()))
                        {
                            if (image.Width > 1024)
                            {
                                image.Mutate(x => x.Resize(1024, 0));
                            }
                            await image.SaveAsync(savePath); 
                        }

                        book.ImageUrl = newFileName;
                    }

                    _context.Update(book);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookExists(book.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["CategoryId"] = new SelectList(_context.Categories, "Id", "Name", book.CategoryId);
            return View(book);
        }

        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var book = await _context.Books
                .Include(b => b.Category)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (book == null) return NotFound();

            return View(book);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book != null)
            {
                _context.Books.Remove(book);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BookExists(int id)
        {
            return _context.Books.Any(e => e.Id == id);
        }

        [Authorize]
        public async Task<IActionResult> Buy(int id, int count=1)
        {
            if(count < 1) return BadRequest();
            
            var book = await _context.Books.FindAsync(id);
            if(book == null) return NotFound();
            
            return RedirectToAction( "Index", "Home");
        }

        public async Task<IActionResult> CommentCreate([Bind("Id,UserName,Content,BookId")] Comment comment)
        {
            comment.CreatedDate = DateTime.Now;

            if (HttpContext.User.Identity.IsAuthenticated)
            {
                comment.UserName = HttpContext.User.Identity.Name;
            }

            _context.Add(comment);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details),new { id = comment.BookId });
        }
    }
}